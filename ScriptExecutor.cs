using FirebirdSql.Data.FirebirdClient;
using System.Text;

namespace DbMetaTool
{
    public class ScriptExecutor(FbConnection connection)
    {
        private readonly FbConnection _connection = connection;

        public void ExecuteScriptsFromDirectory(string scriptsDirectory, string subdirectory)
        {
            string path = Path.Combine(scriptsDirectory, subdirectory);

            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Katalog {subdirectory} nie istnieje, pomijam.");
                return;
            }

            var sqlFiles = Directory.GetFiles(path, "*.sql").OrderBy(f => f).ToArray();

            if (sqlFiles.Length == 0)
            {
                Console.WriteLine($"Brak plików .sql w katalogu {subdirectory}.");
                return;
            }

            int successCount = 0;
            int errorCount = 0;

            foreach (var file in sqlFiles)
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    Console.Write($"  Wykonuję: {fileName}... ");

                    string sqlContent = File.ReadAllText(file, Encoding.UTF8);
                    ExecuteScript(sqlContent);

                    Console.WriteLine("OK");
                    successCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"BŁĄD: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine($"Podsumowanie {subdirectory}: {successCount} sukces, {errorCount} błędów.");
        }

        private void ExecuteScript(string sqlContent)
        {
            if (string.IsNullOrWhiteSpace(sqlContent))
                return;

            bool usesSetTerm = sqlContent.Contains("SET TERM", StringComparison.OrdinalIgnoreCase);

            if (usesSetTerm)
            {
                ExecuteComplexScript(sqlContent);
            }
            else
            {
                ExecuteSimpleScript(sqlContent);
            }
        }

        private void ExecuteSimpleScript(string sqlContent)
        {
            sqlContent = RemoveComments(sqlContent);

            var statements = sqlContent
                .Split(';')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToArray();

            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var statement in statements)
                {
                    using var cmd = new FbCommand(statement, _connection, transaction);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private void ExecuteComplexScript(string sqlContent)
        {
            sqlContent = RemoveComments(sqlContent);

            var statements = ParseScriptWithSetTerm(sqlContent);

            using var transaction = _connection.BeginTransaction();
            try
            {
                foreach (var statement in statements)
                {
                    if (string.IsNullOrWhiteSpace(statement))
                        continue;

                    using var cmd = new FbCommand(statement, _connection, transaction);
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        private static string[] ParseScriptWithSetTerm(string sqlContent)
        {
            var statements = new List<string>();
            string currentTerminator = ";";
            var currentStatement = new StringBuilder();

            var lines = sqlContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("SET TERM", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentStatement.Length > 0)
                    {
                        statements.Add(currentStatement.ToString().Trim());
                        currentStatement.Clear();
                    }

                    var parts = trimmedLine.Split([' '], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        currentTerminator = parts[2].TrimEnd(';');
                    }
                    continue;
                }

                currentStatement.AppendLine(line);

                if (trimmedLine.EndsWith(currentTerminator))
                {
                    string statement = currentStatement.ToString();
                    statement = statement[..statement.LastIndexOf(currentTerminator)].Trim();

                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        statements.Add(statement);
                    }

                    currentStatement.Clear();
                }
            }

            if (currentStatement.Length > 0)
            {
                string statement = currentStatement.ToString().Trim();
                if (statement.EndsWith(currentTerminator))
                {
                    statement = statement[..statement.LastIndexOf(currentTerminator)].Trim();
                }

                if (!string.IsNullOrWhiteSpace(statement))
                {
                    statements.Add(statement);
                }
            }

            return [..statements];
        }

        private static string RemoveComments(string sql)
        {
            var result = new StringBuilder();
            var lines = sql.Split(['\r', '\n'], StringSplitOptions.None);

            foreach (var line in lines)
            {
                int commentIndex = line.IndexOf("--");
                if (commentIndex >= 0)
                {
                    result.AppendLine(line[..commentIndex]);
                }
                else
                {
                    result.AppendLine(line);
                }
            }

            string text = result.ToString();
            while (true)
            {
                int startIndex = text.IndexOf("/*");
                if (startIndex < 0)
                    break;

                int endIndex = text.IndexOf("*/", startIndex);
                if (endIndex < 0)
                    break;

                text = text.Remove(startIndex, endIndex - startIndex + 2);
            }

            return text;
        }
    }
}

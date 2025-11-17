using FirebirdSql.Data.FirebirdClient;
using System.Text;

namespace DbMetaTool
{
    internal class MetadataExporter(FbConnection connection, string outputDirectory)
    {
        private readonly FbConnection _connection = connection;
        private readonly string _outputDirectory = outputDirectory;

        public void ExportDomains()
        {
            string domainsDir = Path.Combine(_outputDirectory, "domains");
            Directory.CreateDirectory(domainsDir);

            string query = @"
                SELECT 
                    RDB$FIELD_NAME,
                    RDB$FIELD_TYPE,
                    RDB$FIELD_LENGTH,
                    RDB$FIELD_PRECISION,
                    RDB$FIELD_SCALE,
                    RDB$CHARACTER_LENGTH,
                    RDB$NULL_FLAG,
                    RDB$DEFAULT_SOURCE
                FROM RDB$FIELDS
                WHERE RDB$FIELD_NAME LIKE 'DOMAIN%'";

            using var cmd = new FbCommand(query, _connection);
            using var reader = cmd.ExecuteReader();

            int count = 0;
            while (reader.Read())
            {
                string domainName = reader["RDB$FIELD_NAME"].ToString()!.Trim();
                string sqlScript = GenerateDomainScript(reader);

                string fileName = Path.Combine(domainsDir, $"{domainName}.sql");
                File.WriteAllText(fileName, sqlScript, Encoding.UTF8);
                count++;
            }

            Console.WriteLine($"Wyeksportowano {count} domen.");
        }

        public void ExportTables()
        {
            string tablesDir = Path.Combine(_outputDirectory, "tables");
            Directory.CreateDirectory(tablesDir);

            string query = @"
                SELECT RDB$RELATION_NAME
                FROM RDB$RELATIONS
                WHERE RDB$SYSTEM_FLAG = 0 
                  AND RDB$VIEW_BLR IS NULL
                ORDER BY RDB$RELATION_NAME";

            using var cmd = new FbCommand(query, _connection);
            using var reader = cmd.ExecuteReader();

            int count = 0;
            while (reader.Read())
            {
                string tableName = reader["RDB$RELATION_NAME"].ToString()!.Trim();
                string sqlScript = GenerateTableScript(tableName);

                string fileName = Path.Combine(tablesDir, $"{tableName}.sql");
                File.WriteAllText(fileName, sqlScript, Encoding.UTF8);
                count++;
            }

            Console.WriteLine($"Wyeksportowano {count} tabel.");
        }

        public void ExportProcedures()
        {
            string proceduresDir = Path.Combine(_outputDirectory, "procedures");
            Directory.CreateDirectory(proceduresDir);

            string query = @"
                SELECT 
                    RDB$PROCEDURE_NAME,
                    RDB$PROCEDURE_SOURCE
                FROM RDB$PROCEDURES
                WHERE RDB$SYSTEM_FLAG = 0
                ORDER BY RDB$PROCEDURE_NAME";

            using var cmd = new FbCommand(query, _connection);
            using var reader = cmd.ExecuteReader();

            int count = 0;
            while (reader.Read())
            {
                string procedureName = reader["RDB$PROCEDURE_NAME"].ToString()!.Trim();
                string procedureSource = reader["RDB$PROCEDURE_SOURCE"]?.ToString() ?? "";

                string sqlScript = GenerateProcedureScript(procedureName, procedureSource);

                string fileName = Path.Combine(proceduresDir, $"{procedureName}.sql");
                File.WriteAllText(fileName, sqlScript, Encoding.UTF8);
                count++;
            }

            Console.WriteLine($"Wyeksportowano {count} procedur.");
        }

        private static string GenerateDomainScript(FbDataReader reader)
        {
            var sb = new StringBuilder();
            string domainName = reader["RDB$FIELD_NAME"].ToString()!.Trim();
            string dataType = GetDataType(reader);

            sb.AppendLine($"CREATE DOMAIN {domainName} AS {dataType}");

            string? defaultSource = reader["RDB$DEFAULT_SOURCE"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(defaultSource))
            {
                sb.AppendLine($"  {defaultSource}");
            }

            object nullFlag = reader["RDB$NULL_FLAG"];
            if (nullFlag != DBNull.Value && Convert.ToInt32(nullFlag) == 1)
            {
                sb.AppendLine("  NOT NULL");
            }

            //string validation = reader["RDB$VALIDATION_SOURCE"]?.ToString()?.Trim();
            //if (!string.IsNullOrEmpty(validation))
            //{
            //    sb.AppendLine($"  {validation}");
            //}

            sb.AppendLine(";");
            return sb.ToString();
        }

        private string GenerateTableScript(string tableName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {tableName} (");

            string query = @"
                SELECT 
                    rf.RDB$FIELD_NAME,
                    rf.RDB$FIELD_SOURCE,
                    f.RDB$FIELD_TYPE,
                    f.RDB$FIELD_LENGTH,
                    f.RDB$FIELD_PRECISION,
                    f.RDB$FIELD_SCALE,
                    f.RDB$CHARACTER_LENGTH,
                    rf.RDB$NULL_FLAG,
                    rf.RDB$DEFAULT_SOURCE,
                    rf.RDB$FIELD_POSITION
                FROM RDB$RELATION_FIELDS rf
                JOIN RDB$FIELDS f ON rf.RDB$FIELD_SOURCE = f.RDB$FIELD_NAME
                WHERE rf.RDB$RELATION_NAME = @TableName
                ORDER BY rf.RDB$FIELD_POSITION";

            using var cmd = new FbCommand(query, _connection);
            cmd.Parameters.AddWithValue("@TableName", tableName);
            using var reader = cmd.ExecuteReader();

            bool first = true;
            while (reader.Read())
            {
                if (!first)
                {
                    sb.AppendLine(",");
                }

                first = false;

                string fieldName = reader["RDB$FIELD_NAME"].ToString()!.Trim();
                string dataType = GetDataType(reader);

                sb.Append($"  {fieldName} {dataType}");

                object nullFlag = reader["RDB$NULL_FLAG"];
                if (nullFlag != DBNull.Value && Convert.ToInt32(nullFlag) == 1)
                {
                    sb.Append(" NOT NULL");
                }

                string? defaultSource = reader["RDB$DEFAULT_SOURCE"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(defaultSource))
                {
                    sb.Append($" {defaultSource}");
                }
            }

            sb.AppendLine();
            sb.AppendLine(");");
            return sb.ToString();
        }

        private string GenerateProcedureScript(string procedureName, string procedureSource)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SET TERM ^ ;");
            sb.AppendLine();

            string query = @"
                SELECT 
                    RDB$PROCEDURE_NAME,
                    RDB$PROCEDURE_SOURCE
                FROM RDB$PROCEDURES
                WHERE RDB$PROCEDURE_NAME = @ProcName";

            using var cmd = new FbCommand(query, _connection);
            cmd.Parameters.AddWithValue("@ProcName", procedureName);

            string paramsQuery = @"
                SELECT 
                    RDB$PARAMETER_NAME,
                    RDB$FIELD_SOURCE,
                    RDB$PARAMETER_TYPE
                FROM RDB$PROCEDURE_PARAMETERS
                WHERE RDB$PROCEDURE_NAME = @ProcName
                ORDER BY RDB$PARAMETER_NUMBER";

            sb.AppendLine($"CREATE PROCEDURE {procedureName}");

            using var paramCmd = new FbCommand(paramsQuery, _connection);
            paramCmd.Parameters.AddWithValue("@ProcName", procedureName);
            using var paramReader = paramCmd.ExecuteReader();

            bool hasInputParams = false;
            bool hasOutputParams = false;
            var inputParams = new StringBuilder();
            var outputParams = new StringBuilder();

            while (paramReader.Read())
            {
                string paramName = paramReader["RDB$PARAMETER_NAME"].ToString()!.Trim();
                string paramType = paramReader["RDB$FIELD_SOURCE"].ToString()!.Trim();
                int paramDirection = Convert.ToInt32(paramReader["RDB$PARAMETER_TYPE"]);

                if (paramDirection == 0) // Input parameter
                {
                    if (hasInputParams)
                        inputParams.Append(", ");
                    inputParams.Append($"{paramName} {paramType}");
                    hasInputParams = true;
                }
                else // Output parameter
                {
                    if (hasOutputParams)
                        outputParams.Append(", ");
                    outputParams.Append($"{paramName} {paramType}");
                    hasOutputParams = true;
                }
            }

            if (hasInputParams)
                sb.AppendLine($"  ({inputParams})");

            if (hasOutputParams)
                sb.AppendLine($"RETURNS ({outputParams})");

            sb.AppendLine("AS");
            sb.AppendLine(procedureSource);
            sb.AppendLine("^");
            sb.AppendLine();
            sb.AppendLine("SET TERM ; ^");

            return sb.ToString();
        }

        private static string GetDataType(FbDataReader reader)
        {
            int fieldType = Convert.ToInt32(reader["RDB$FIELD_TYPE"]);
            int fieldLength = reader["RDB$FIELD_LENGTH"] != DBNull.Value ? Convert.ToInt32(reader["RDB$FIELD_LENGTH"]) : 0;
            int fieldScale = reader["RDB$FIELD_SCALE"] != DBNull.Value ? Convert.ToInt32(reader["RDB$FIELD_SCALE"]) : 0;
            int charLength = reader["RDB$CHARACTER_LENGTH"] != DBNull.Value ? Convert.ToInt32(reader["RDB$CHARACTER_LENGTH"]) : 0;

            return fieldType switch
            {
                8 => fieldScale < 0 ? $"NUMERIC(15,{-fieldScale})" : "INTEGER",
                12 => "DATE",
                13 => "TIME",
                14 => charLength > 0 ? $"CHAR({charLength})" : $"CHAR({fieldLength})",
                16 => fieldScale < 0 ? $"NUMERIC(18,{-fieldScale})" : "BIGINT",
                23 => "BOOLEAN",
                35 => "TIMESTAMP",
                37 => charLength > 0 ? $"VARCHAR({charLength})" : $"VARCHAR({fieldLength})",
                _ => $"UNKNOWN_TYPE_{fieldType}"
            };
        }
    }
}

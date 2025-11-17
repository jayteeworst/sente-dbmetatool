# DbMetaTool - Narzędzie do zarządzania metadanymi Firebird 5.0
DbMetaTool to aplikacja konsolowa .NET 8.0 służąca do generowania skryptów metadanych z bazy danych Firebird 5.0 do plików .sql.

## Wymagania

.NET 8.0 SDK

Firebird 5.0 Server

Pakiet NuGet: FirebirdSql.Data.FirebirdClient (v10.3.1)

## Sposób użycia
1. Budowanie bazy danych ze skryptów: `DbMetaTool build-db --db-dir "<ścieżka_docelowa>" --scripts-dir "<folder_ze_skryptami>"`, gdzie:
- --db-dir - katalog, w którym ma powstać baza danych (plik database.fdb)
- --scripts-dir - katalog zawierający podkatalogi: domains, tables, procedures

2. Generowanie skryptów z bazy danych: `DbMetaTool export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=C:\<dir>\<plik>.fdb;ServerType=1" --output-dir "<ścieżka_docelowa>"`, gdzie:
- --connection-string - parametry połączenia do bazy Firebird
- --output-dir - katalog docelowy dla wygenerowanych skryptów

3. Aktualizowanie istniejącej bazy danych na podstawie skryptów: `DbMetaTool build-db --db-dir "<ściezka_do_bazy_danych>" --scripts-dir "<folder_ze_skryptami>"`, gdzie:
- --connection-string - parametry połączenia do bazy Firebird
- --scripts-dir - katalog zawierający skrypty do wykonania

## Kolejność wykonywania skryptów
Aplikacja wykonuje skrypty w następującej kolejności:

- Domeny - definicje typów danych
- Tabele - struktury tabel z polami
- Procedury - procedury składowane

## Obsługiwane typy danych
`INTEGER` - 32-bitowa liczba całkowita

`BIGINT` - 64-bitowa liczba całkowita

`NUMERIC(p,s)` - liczba o stałej precyzji

`CHAR(n)` - łańcuch o stałej długości

`VARCHAR(n)` - łańcuch o zmiennej długości

`DATE` - data

`TIME` - czas

`TIMESTAMP` - data i czas

`BOOLEAN` - wartość logiczna (TRUE/FALSE)

## Obsługa błędów
Aplikacja:

- Wyświetla szczegółowe informacje o błędach
- Używa transakcji przy wykonywaniu skryptów
- Rollback w przypadku błędu
- Raportuje liczbę przetworzonych obiektów

## Uwagi

- Domyślne dane logowania: `SYSDBA / masterkey`
- Baza jest tworzona z kodowaniem UTF-8
- Procedury mogą używać składni SET TERM dla rozdzielania poleceń
- Aplikacja nie obsługuje: constraints, triggerów, indeksów, widoków, generatorów

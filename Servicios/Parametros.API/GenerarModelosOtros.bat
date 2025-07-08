@echo off
echo ==============================================
echo Generando Modelos y DbContext desde la Base de Datos (.NET 6)
echo ==============================================

:: Ruta base del proyecto
cd /d C:\Johan\Proyectos\Propios\AgiTime\Servicios\Parametros.API

:: Crear carpetas si no existen
if not exist "Data\Modelos" mkdir Data\Modelos
if not exist "Data" mkdir Data

:: Cadena de conexión
set CADENA="Server=JOHAN\SQLEXPRESS;Database=AgiTimePrueba;User Id=sa;Password=123456;TrustServerCertificate=True;"
:: Proveedor EF Core
set PROVIDER=Microsoft.EntityFrameworkCore.SqlServer

:: Ejecutar scaffold solo con las tablas necesarias
dotnet ef dbcontext scaffold %CADENA% %PROVIDER% ^
--output-dir Data\Modelos ^
--context-dir Data ^
--context ParametrosContext ^
--use-database-names ^
--no-onconfiguring ^
--force ^
--table TipoDocumento ^
--table Genero ^
--table Ciudad ^
--table Usuarios ^
--table Rol ^
--table Sede

echo ==============================================
echo ✅ Generación completada correctamente
pause

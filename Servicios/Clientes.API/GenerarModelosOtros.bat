@echo off
echo ==============================================
echo Generando Modelos y DbContext desde la Base de Datos (.NET 6)
echo ==============================================

:: Ruta base del proyecto
cd /d C:\Johan\Proyectos\Propios\AgiTime\Servicios\Cientes.API

:: Crear carpetas si no existen
if not exist "Data\Modelos" mkdir Data\Modelos
if not exist "Data" mkdir Data

:: Cadena de conexión
set CADENA="Server=178.156.162.97,1433;Database=AgiTimePruebas;User Id=sa;Password=@giTime!;TrustServerCertificate=True;"
:: Proveedor EF Core
set PROVIDER=Microsoft.EntityFrameworkCore.SqlServer

:: Ejecutar scaffold solo con las tablas necesarias
dotnet ef dbcontext scaffold %CADENA% %PROVIDER% ^
--output-dir Data\Modelos ^
--context-dir Data ^
--context ModeloContext ^
--use-database-names ^
--no-onconfiguring ^
--force ^
--table Agenda ^
--table BloqueosHorario ^
--table Citas ^
--table Ciudad ^
--table Clientes ^
--table DetalleProducto ^
--table Funcionalidades ^
--table FuncionalidadesSet ^
--table Genero ^
--table Logs ^
--table Parametros ^
--table Producto ^
--table Recursos ^
--table Rol ^
--table Sede ^
--table Servicios ^
--table TipoDocumento ^
--table UsuarioRol ^
--table Usuarios ^
--table Venta ^
--table VentaDetalle

echo ==============================================
echo ✅ Generación completada correctamente
pause

@echo off
echo ==============================================
echo Generando Modelos de Tablas Especificas desde la Base de Datos...
echo ==============================================

dotnet ef dbcontext scaffold "Server=JOHAN\SQLEXPRESS;Database=AgiTime;User=sa;Password=123456;Encrypt=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer -o Models -f -t Clientes -t Sede -t Empresa -t EmpresaCliente -t Ciudad -t TipoDocumento --no-onconfiguring

echo ==============================================
echo Modelos de tablas especificas generados correctamente
pause

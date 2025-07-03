@echo off
echo ==============================================
echo Generando Modelos de Tablas Especificas desde la Base de Datos...
echo ==============================================

dotnet ef dbcontext scaffold "Server=JOHAN\SQLEXPRESS;Database=AgiTime;User=sa;Password=123456;Encrypt=True;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer -o Models -f -t USUARIOS -t ROLES --no-onconfiguring

echo ==============================================
echo Modelos de tablas especificas generados correctamente
pause

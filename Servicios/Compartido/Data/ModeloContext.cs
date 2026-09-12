using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Compartido.Data.Modelos;

namespace Compartido.Data
{
    public partial class ModeloContext : DbContext
    {
        public ModeloContext()
        {
        }

        public ModeloContext(DbContextOptions<ModeloContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Agendum> Agenda { get; set; }
        public virtual DbSet<BloqueosHorario> BloqueosHorarios { get; set; }
        public virtual DbSet<Cita> Citas { get; set; }
        public virtual DbSet<CitaPadre> CitaPadres { get; set; }
        public virtual DbSet<CitaServicio> CitaServicios { get; set; }
        public virtual DbSet<Ciudad> Ciudads { get; set; }
        public virtual DbSet<Cliente> Clientes { get; set; }
        public virtual DbSet<Funcionalidade> Funcionalidades { get; set; }
        public virtual DbSet<FuncionalidadesSet> FuncionalidadesSets { get; set; }
        public virtual DbSet<Genero> Generos { get; set; }
        public virtual DbSet<IngresoMesPrueba> IngresoMesPruebas { get; set; }
        public virtual DbSet<LiquidacionCitum> LiquidacionCita { get; set; }
        public virtual DbSet<Log> Logs { get; set; }
        public virtual DbSet<MetodosPago> MetodosPagos { get; set; }
        public virtual DbSet<Ordene> Ordenes { get; set; }
        public virtual DbSet<Padre> Padres { get; set; }
        public virtual DbSet<Parametro> Parametros { get; set; }
        public virtual DbSet<Producto> Productos { get; set; }
        public virtual DbSet<ProductoLote> ProductoLotes { get; set; }
        public virtual DbSet<Recurso> Recursos { get; set; }
        public virtual DbSet<Reporte> Reportes { get; set; }
        public virtual DbSet<Rol> Rols { get; set; }
        public virtual DbSet<Sede> Sedes { get; set; }
        public virtual DbSet<Servicio> Servicios { get; set; }
        public virtual DbSet<ServiciosParametro> ServiciosParametros { get; set; }
        public virtual DbSet<ServiciosRecurso> ServiciosRecursos { get; set; }
        public virtual DbSet<Sesione> Sesiones { get; set; }
        public virtual DbSet<TipoDocumento> TipoDocumentos { get; set; }
        public virtual DbSet<Usuario> Usuarios { get; set; }
        public virtual DbSet<VentaDetalle> VentaDetalles { get; set; }
        public virtual DbSet<VentaDetalleLote> VentaDetalleLotes { get; set; }
        public virtual DbSet<Ventum> Venta { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Agendum>(entity =>
            {
                entity.Property(e => e.valor).HasColumnType("time(0)");
            });

            modelBuilder.Entity<BloqueosHorario>(entity =>
            {
                entity.ToTable("BloqueosHorario");

                entity.Property(e => e.diasSemana)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.fechaFin).HasColumnType("datetime");

                entity.Property(e => e.fechaInicio).HasColumnType("datetime");

                entity.Property(e => e.horaFin).HasDefaultValueSql("('23:59')");

                entity.Property(e => e.horaInicio).HasDefaultValueSql("('00:00')");

                entity.Property(e => e.motivo)
                    .IsRequired()
                    .IsUnicode(false);

                entity.Property(e => e.tipoBloqueo).HasDefaultValueSql("((3))");

                entity.HasOne(d => d.usuarioNavigation)
                    .WithMany(p => p.BloqueosHorarios)
                    .HasForeignKey(d => d.usuario)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_BloqueosHorario_Usuarios");
            });

            modelBuilder.Entity<Cita>(entity =>
            {
                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCancelacion).HasColumnType("datetime");

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.fechaFin).HasColumnType("datetime");

                entity.Property(e => e.fechaInicio).HasColumnType("datetime");

                entity.Property(e => e.metodoPago)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.pago).HasDefaultValueSql("((0))");

                entity.Property(e => e.usuarioCancelacion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.usuarioCreacion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.valor).HasColumnType("decimal(18, 0)");
            });

            modelBuilder.Entity<CitaPadre>(entity =>
            {
                entity.ToTable("CitaPadre");

                entity.HasOne(d => d.citaNavigation)
                    .WithMany(p => p.CitaPadres)
                    .HasForeignKey(d => d.cita)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CitaPadre_Citas");

                entity.HasOne(d => d.padreNavigation)
                    .WithMany(p => p.CitaPadres)
                    .HasForeignKey(d => d.padre)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CitaPadre_Padre");
            });

            modelBuilder.Entity<CitaServicio>(entity =>
            {
                entity.HasOne(d => d.citaNavigation)
                    .WithMany(p => p.CitaServicios)
                    .HasForeignKey(d => d.cita)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CitaServicios_Citas");

                entity.HasOne(d => d.servicioNavigation)
                    .WithMany(p => p.CitaServicios)
                    .HasForeignKey(d => d.servicio)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_CitaServicios_Servicios");
            });

            modelBuilder.Entity<Ciudad>(entity =>
            {
                entity.ToTable("Ciudad");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.Property(e => e.correo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.documento)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.fechaNacimiento).HasColumnType("datetime");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.HasOne(d => d.generoNavigation)
                    .WithMany(p => p.Clientes)
                    .HasForeignKey(d => d.genero)
                    .HasConstraintName("FK_Clientes_Genero");

                entity.HasOne(d => d.tipoDocumentoNavigation)
                    .WithMany(p => p.Clientes)
                    .HasForeignKey(d => d.tipoDocumento)
                    .HasConstraintName("FK_Clientes_TipoDocumento");
            });

            modelBuilder.Entity<Funcionalidade>(entity =>
            {
                entity.Property(e => e.codigo)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.funcionalidad).IsUnicode(false);
            });

            modelBuilder.Entity<FuncionalidadesSet>(entity =>
            {
                entity.ToTable("FuncionalidadesSet");

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(10)
                    .IsUnicode(false);

                entity.HasOne(d => d.funcionalidadNavigation)
                    .WithMany(p => p.FuncionalidadesSets)
                    .HasForeignKey(d => d.funcionalidad)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_FuncionalidadesSet_Funcionalidades");

                entity.HasOne(d => d.rolNavigation)
                    .WithMany(p => p.FuncionalidadesSets)
                    .HasForeignKey(d => d.rol)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_FuncionalidadesSet_Rol");
            });

            modelBuilder.Entity<Genero>(entity =>
            {
                entity.ToTable("Genero");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<IngresoMesPrueba>(entity =>
            {
                entity.ToTable("IngresoMesPrueba");

                entity.Property(e => e.fecha).HasColumnType("datetime");

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.sede)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.usuarioCreacion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.valorBarberia).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.valorBarbero).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.valorServicio).HasColumnType("decimal(18, 0)");

                entity.HasOne(d => d.recursoNavigation)
                    .WithMany(p => p.IngresoMesPruebas)
                    .HasForeignKey(d => d.recurso)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_IngresoMesPrueba_Usuarios");
            });

            modelBuilder.Entity<LiquidacionCitum>(entity =>
            {
                entity.Property(e => e.fechaCita).HasColumnType("datetime");

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.sede)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.tipo)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.usuarioCreacion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.valorBarberia).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.valorBarbero).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.valorServicio).HasColumnType("decimal(18, 0)");

                entity.HasOne(d => d.citaNavigation)
                    .WithMany(p => p.LiquidacionCita)
                    .HasForeignKey(d => d.cita)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_LiquidacionCita_Citas");
            });

            modelBuilder.Entity<Log>(entity =>
            {
                entity.Property(e => e.Level).HasMaxLength(50);

                entity.Property(e => e.Timestamp).HasColumnType("datetime");
            });

            modelBuilder.Entity<MetodosPago>(entity =>
            {
                entity.ToTable("MetodosPago");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Ordene>(entity =>
            {
                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.observaciones).IsUnicode(false);

                entity.Property(e => e.usuarioCreacion)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Padre>(entity =>
            {
                entity.ToTable("Padre");

                entity.Property(e => e.fechaCita).HasColumnType("datetime");
            });

            modelBuilder.Entity<Parametro>(entity =>
            {
                entity.Property(e => e.observacion)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.tipo)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.valor)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Producto>(entity =>
            {
                entity.ToTable("Producto");

                entity.Property(e => e.codigo)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.porcentaje).HasColumnType("decimal(18, 2)");

                entity.Property(e => e.precioVenta).HasColumnType("decimal(18, 2)");
            });

            modelBuilder.Entity<ProductoLote>(entity =>
            {
                entity.ToTable("ProductoLote");

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.fechaIngreso)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.precioCosto).HasColumnType("decimal(18, 2)");

                entity.HasOne(d => d.productoNavigation)
                    .WithMany(p => p.ProductoLotes)
                    .HasForeignKey(d => d.producto)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_ProductoLote_Producto");
            });

            modelBuilder.Entity<Recurso>(entity =>
            {
                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.HasOne(d => d.sedeNavigation)
                    .WithMany(p => p.Recursos)
                    .HasForeignKey(d => d.sede)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Recursos_Sede");
            });

            modelBuilder.Entity<Reporte>(entity =>
            {
                entity.Property(e => e.descripcion)
                    .HasMaxLength(500)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.procedimiento)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Rol>(entity =>
            {
                entity.ToTable("Rol");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Sede>(entity =>
            {
                entity.ToTable("Sede");

                entity.Property(e => e.Nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.barrio)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.direccion)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.foto)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.latitud)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.longitud)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Servicio>(entity =>
            {
                entity.Property(e => e.esPago).HasDefaultValueSql("((1))");

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.observaciones).IsUnicode(false);

                entity.Property(e => e.valor).HasColumnType("decimal(18, 0)");

                entity.HasOne(d => d.sedeNavigation)
                    .WithMany(p => p.Servicios)
                    .HasForeignKey(d => d.sede)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Servicios_Sede");
            });

            modelBuilder.Entity<ServiciosParametro>(entity =>
            {
                entity.Property(e => e.porcentaje).HasColumnType("decimal(5, 0)");

                entity.HasOne(d => d.servicioNavigation)
                    .WithMany(p => p.ServiciosParametros)
                    .HasForeignKey(d => d.servicio)
                    .HasConstraintName("FK_ServiciosParametros_Servicios");
            });

            modelBuilder.Entity<ServiciosRecurso>(entity =>
            {
                entity.HasOne(d => d.servicioNavigation)
                    .WithMany(p => p.ServiciosRecursos)
                    .HasForeignKey(d => d.servicio)
                    .HasConstraintName("FK_ServiciosRecursos_Servicios");
            });

            modelBuilder.Entity<Sesione>(entity =>
            {
                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.HasOne(d => d.ordenNavigation)
                    .WithMany(p => p.Sesiones)
                    .HasForeignKey(d => d.orden)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Sesiones_Ordenes");
            });

            modelBuilder.Entity<TipoDocumento>(entity =>
            {
                entity.ToTable("TipoDocumento");

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(e => e.correo)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.documento)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.fechaNacimiento).HasColumnType("datetime");

                entity.Property(e => e.foto)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.incumplimiento).HasColumnType("decimal(18, 1)");

                entity.Property(e => e.login)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.password)
                    .IsRequired()
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.porcentaje).HasColumnType("decimal(18, 1)");

                entity.Property(e => e.telefono)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.HasOne(d => d.generoNavigation)
                    .WithMany(p => p.Usuarios)
                    .HasForeignKey(d => d.genero)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Usuarios_Genero");

                entity.HasOne(d => d.sedeNavigation)
                    .WithMany(p => p.Usuarios)
                    .HasForeignKey(d => d.sede)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Usuarios_Sede");

                entity.HasOne(d => d.tipoDocumentoNavigation)
                    .WithMany(p => p.Usuarios)
                    .HasForeignKey(d => d.tipoDocumento)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Usuarios_TipoDocumento");

                entity.HasMany(d => d.idRols)
                    .WithMany(p => p.idUsuarios)
                    .UsingEntity<Dictionary<string, object>>(
                        "UsuarioRol",
                        l => l.HasOne<Rol>().WithMany().HasForeignKey("idRol").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_UsuarioRol_Rol"),
                        r => r.HasOne<Usuario>().WithMany().HasForeignKey("idUsuario").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_UsuarioRol_Usuarios"),
                        j =>
                        {
                            j.HasKey("idUsuario", "idRol").HasName("PK__UsuarioR__179F51515880FECF");

                            j.ToTable("UsuarioRol");
                        });
            });

            modelBuilder.Entity<VentaDetalle>(entity =>
            {
                entity.ToTable("VentaDetalle");

                entity.Property(e => e.comision).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.descuento).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.total).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.valorVenta).HasColumnType("decimal(18, 0)");

                entity.HasOne(d => d.productoNavigation)
                    .WithMany(p => p.VentaDetalles)
                    .HasForeignKey(d => d.producto)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_VentaDetalle_Producto");

                entity.HasOne(d => d.ventaNavigation)
                    .WithMany(p => p.VentaDetalles)
                    .HasForeignKey(d => d.venta)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_VentaDetalle_Venta");
            });

            modelBuilder.Entity<VentaDetalleLote>(entity =>
            {
                entity.ToTable("VentaDetalleLote");

                entity.HasOne(d => d.productoLoteNavigation)
                    .WithMany(p => p.VentaDetalleLotes)
                    .HasForeignKey(d => d.productoLote)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_VentaDetalleLote_ProductoLote");

                entity.HasOne(d => d.ventaDetalleNavigation)
                    .WithMany(p => p.VentaDetalleLotes)
                    .HasForeignKey(d => d.ventaDetalle)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_VentaDetalleLote_VentaDetalle");
            });

            modelBuilder.Entity<Ventum>(entity =>
            {
                entity.Property(e => e.factura)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.fechaVenta).HasColumnType("datetime");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

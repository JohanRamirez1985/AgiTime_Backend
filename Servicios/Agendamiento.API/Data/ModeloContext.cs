using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Agendamiento.API.Data.Modelos;

namespace Agendamiento.API.Data
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

        public virtual DbSet<Agendum> Agenda { get; set; } = null!;
        public virtual DbSet<BloqueosHorario> BloqueosHorarios { get; set; } = null!;
        public virtual DbSet<Cita> Citas { get; set; } = null!;
        public virtual DbSet<CitaServicio> CitaServicios { get; set; } = null!;
        public virtual DbSet<Ciudad> Ciudads { get; set; } = null!;
        public virtual DbSet<Cliente> Clientes { get; set; } = null!;
        public virtual DbSet<DetalleProducto> DetalleProductos { get; set; } = null!;
        public virtual DbSet<Funcionalidade> Funcionalidades { get; set; } = null!;
        public virtual DbSet<FuncionalidadesSet> FuncionalidadesSets { get; set; } = null!;
        public virtual DbSet<Genero> Generos { get; set; } = null!;
        public virtual DbSet<Log> Logs { get; set; } = null!;
        public virtual DbSet<Parametro> Parametros { get; set; } = null!;
        public virtual DbSet<Producto> Productos { get; set; } = null!;
        public virtual DbSet<Recurso> Recursos { get; set; } = null!;
        public virtual DbSet<Rol> Rols { get; set; } = null!;
        public virtual DbSet<Sede> Sedes { get; set; } = null!;
        public virtual DbSet<Servicio> Servicios { get; set; } = null!;
        public virtual DbSet<TipoDocumento> TipoDocumentos { get; set; } = null!;
        public virtual DbSet<Usuario> Usuarios { get; set; } = null!;
        public virtual DbSet<VentaDetalle> VentaDetalles { get; set; } = null!;
        public virtual DbSet<Ventum> Venta { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BloqueosHorario>(entity =>
            {
                entity.ToTable("BloqueosHorario");

                entity.Property(e => e.diasSemana)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion)
                    .HasColumnType("datetime")
                    .HasDefaultValueSql("(getdate())");

                entity.Property(e => e.fechaFin).HasColumnType("datetime");

                entity.Property(e => e.fechaInicio).HasColumnType("datetime");

                entity.Property(e => e.horaFin).HasDefaultValueSql("('23:59')");

                entity.Property(e => e.horaInicio).HasDefaultValueSql("('00:00')");

                entity.Property(e => e.motivo).IsUnicode(false);

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
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCancelacion).HasColumnType("datetime");

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.fechaFin).HasColumnType("datetime");

                entity.Property(e => e.fechaInicio).HasColumnType("datetime");

                entity.Property(e => e.usuarioCancelacion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.usuarioCreacion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.valor).HasColumnType("decimal(18, 0)");
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
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.Property(e => e.correo)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.documento)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.fechaNacimiento).HasColumnType("datetime");

                entity.Property(e => e.nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
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

            modelBuilder.Entity<DetalleProducto>(entity =>
            {
                entity.ToTable("DetalleProducto");

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.HasOne(d => d.productoNavigation)
                    .WithMany(p => p.DetalleProductos)
                    .HasForeignKey(d => d.producto)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_DetalleProducto_Producto");

                entity.HasOne(d => d.usuarioCreacionNavigation)
                    .WithMany(p => p.DetalleProductos)
                    .HasForeignKey(d => d.usuarioCreacion)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_DetalleProducto_Usuarios");
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
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Log>(entity =>
            {
                entity.Property(e => e.Level).HasMaxLength(50);

                entity.Property(e => e.Timestamp).HasColumnType("datetime");
            });

            modelBuilder.Entity<Parametro>(entity =>
            {
                entity.Property(e => e.tipo)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.valor)
                    .HasMaxLength(100)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Producto>(entity =>
            {
                entity.ToTable("Producto");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.codigo)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.precio).HasColumnType("decimal(18, 0)");
            });

            modelBuilder.Entity<Recurso>(entity =>
            {
                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.HasOne(d => d.sedeNavigation)
                    .WithMany(p => p.Recursos)
                    .HasForeignKey(d => d.sede)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Recursos_Sede");
            });

            modelBuilder.Entity<Rol>(entity =>
            {
                entity.ToTable("Rol");

                entity.Property(e => e.nombre)
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Sede>(entity =>
            {
                entity.ToTable("Sede");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.barrio)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.direccion)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Servicio>(entity =>
            {
                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.valor).HasColumnType("decimal(18, 0)");
            });

            modelBuilder.Entity<TipoDocumento>(entity =>
            {
                entity.ToTable("TipoDocumento");

                entity.Property(e => e.nombre)
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.Property(e => e.correo)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.documento)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.fechaCreacion).HasColumnType("datetime");

                entity.Property(e => e.fechaNacimiento).HasColumnType("datetime");

                entity.Property(e => e.foto)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.login)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.password)
                    .HasMaxLength(200)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
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
                            j.HasKey("idUsuario", "idRol").HasName("PK__UsuarioR__179F515182DEE9FE");

                            j.ToTable("UsuarioRol");
                        });
            });

            modelBuilder.Entity<VentaDetalle>(entity =>
            {
                entity.ToTable("VentaDetalle");

                entity.Property(e => e.total).HasColumnType("decimal(18, 0)");

                entity.Property(e => e.valor).HasColumnType("decimal(18, 0)");

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

            modelBuilder.Entity<Ventum>(entity =>
            {
                entity.Property(e => e.factura)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.fechaVenta).HasColumnType("datetime");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

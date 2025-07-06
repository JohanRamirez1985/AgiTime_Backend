using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Clientes.API.Models
{
    public partial class AgiTimeContext : DbContext
    {
        public AgiTimeContext()
        {
        }

        public AgiTimeContext(DbContextOptions<AgiTimeContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Ciudad> Ciudads { get; set; } = null!;
        public virtual DbSet<Cliente> Clientes { get; set; } = null!;
        public virtual DbSet<Empresa> Empresas { get; set; } = null!;
        public virtual DbSet<EmpresaCliente> EmpresaClientes { get; set; } = null!;
        public virtual DbSet<Sede> Sedes { get; set; } = null!;
        public virtual DbSet<TipoDocumento> TipoDocumentos { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ciudad>(entity =>
            {
                entity.ToTable("Ciudad");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("nombre");
            });

            modelBuilder.Entity<Cliente>(entity =>
            {
                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Correo)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("correo");

                entity.Property(e => e.Documento)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("documento");

                entity.Property(e => e.Estado)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("estado");

                entity.Property(e => e.FechaNacimiento)
                    .HasColumnType("datetime")
                    .HasColumnName("fechaNacimiento");

                entity.Property(e => e.Genero)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("genero");

                entity.Property(e => e.Login)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("login");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("nombre");

                entity.Property(e => e.Password)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("password");

                entity.Property(e => e.Telefono)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("telefono");

                entity.Property(e => e.TipoDocumento).HasColumnName("tipoDocumento");

                entity.HasOne(d => d.TipoDocumentoNavigation)
                    .WithMany(p => p.Clientes)
                    .HasForeignKey(d => d.TipoDocumento)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Clientes_TipoDocumento");
            });

            modelBuilder.Entity<Empresa>(entity =>
            {
                entity.ToTable("Empresa");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.ConexionBd)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("conexionBD");

                entity.Property(e => e.Estado)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("estado");

                entity.Property(e => e.Logo)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("logo");

                entity.Property(e => e.Nit)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("nit");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("nombre");
            });

            modelBuilder.Entity<EmpresaCliente>(entity =>
            {
                entity.ToTable("EmpresaCliente");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.FechaRegistro)
                    .HasColumnType("datetime")
                    .HasColumnName("fechaRegistro");

                entity.Property(e => e.IdCliente).HasColumnName("idCliente");

                entity.Property(e => e.IdEmpresa).HasColumnName("idEmpresa");

                entity.HasOne(d => d.IdClienteNavigation)
                    .WithMany(p => p.EmpresaClientes)
                    .HasForeignKey(d => d.IdCliente)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_EmpresaCliente_Clientes");

                entity.HasOne(d => d.IdEmpresaNavigation)
                    .WithMany(p => p.EmpresaClientes)
                    .HasForeignKey(d => d.IdEmpresa)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_EmpresaCliente_Empresa");
            });

            modelBuilder.Entity<Sede>(entity =>
            {
                entity.ToTable("Sede");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Barrio)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("barrio");

                entity.Property(e => e.Direccion)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("direccion");

                entity.Property(e => e.Estado)
                    .HasMaxLength(20)
                    .IsUnicode(false)
                    .HasColumnName("estado");

                entity.Property(e => e.IdCiudad).HasColumnName("idCiudad");

                entity.Property(e => e.IdEmpresa).HasColumnName("idEmpresa");

                entity.Property(e => e.Telefono)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("telefono");

                entity.HasOne(d => d.IdCiudadNavigation)
                    .WithMany(p => p.Sedes)
                    .HasForeignKey(d => d.IdCiudad)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Sede_Sede");

                entity.HasOne(d => d.IdEmpresaNavigation)
                    .WithMany(p => p.Sedes)
                    .HasForeignKey(d => d.IdEmpresa)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Sede_Empresa");
            });

            modelBuilder.Entity<TipoDocumento>(entity =>
            {
                entity.ToTable("TipoDocumento");

                entity.Property(e => e.Id).HasColumnName("id");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("nombre");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

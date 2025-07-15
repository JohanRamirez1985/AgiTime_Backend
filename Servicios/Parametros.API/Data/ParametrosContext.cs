using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Parametros.API.Data.Modelos;

namespace Parametros.API.Data
{
    public partial class ParametrosContext : DbContext
    {
        public ParametrosContext()
        {
        }

        public ParametrosContext(DbContextOptions<ParametrosContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Ciudad> Ciudads { get; set; } = null!;
        public virtual DbSet<Genero> Generos { get; set; } = null!;
        public virtual DbSet<Rol> Rols { get; set; } = null!;
        public virtual DbSet<Sede> Sedes { get; set; } = null!;
        public virtual DbSet<TipoDocumento> TipoDocumentos { get; set; } = null!;
        public virtual DbSet<Usuario> Usuarios { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ciudad>(entity =>
            {
                entity.ToTable("Ciudad");

                entity.Property(e => e.nombre)
                    .HasMaxLength(50)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<Genero>(entity =>
            {
                entity.ToTable("Genero");

                entity.Property(e => e.nombre)
                    .HasMaxLength(50)
                    .IsUnicode(false);
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
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

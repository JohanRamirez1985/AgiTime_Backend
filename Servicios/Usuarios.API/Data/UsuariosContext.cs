using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Usuarios.API.Data.Modelos;

namespace Usuarios.API.Data
{
    public partial class UsuariosContext : DbContext
    {
        public UsuariosContext()
        {
        }

        public UsuariosContext(DbContextOptions<UsuariosContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Rol> Rols { get; set; } = null!;
        public virtual DbSet<Sede> Sedes { get; set; } = null!;
        public virtual DbSet<Usuario> Usuarios { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.HasOne(d => d.sedeNavigation)
                    .WithMany(p => p.Usuarios)
                    .HasForeignKey(d => d.sede)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Usuarios_Sede");

                entity.HasMany(d => d.idRols)
                    .WithMany(p => p.idUsuarios)
                    .UsingEntity<Dictionary<string, object>>(
                        "UsuarioRol",
                        l => l.HasOne<Rol>().WithMany().HasForeignKey("idRol").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_UsuarioRol_Rol"),
                        r => r.HasOne<Usuario>().WithMany().HasForeignKey("idUsuario").OnDelete(DeleteBehavior.ClientSetNull).HasConstraintName("FK_UsuarioRol_Usuarios"),
                        j =>
                        {
                            j.HasKey("idUsuario", "idRol").HasName("PK__UsuarioR__179F51512D27B809");

                            j.ToTable("UsuarioRol");
                        });
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

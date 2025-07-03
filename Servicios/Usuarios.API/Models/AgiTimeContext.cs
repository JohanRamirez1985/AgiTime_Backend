using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Usuarios.API.Models
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

        public virtual DbSet<Role> Roles { get; set; } = null!;
        public virtual DbSet<Usuario> Usuarios { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("ROLES");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("NOMBRE");
            });

            modelBuilder.Entity<Usuario>(entity =>
            {
                entity.ToTable("USUARIOS");

                entity.Property(e => e.Id).HasColumnName("ID");

                entity.Property(e => e.Bd)
                    .HasMaxLength(200)
                    .IsUnicode(false)
                    .HasColumnName("BD");

                entity.Property(e => e.Celular)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("CELULAR");

                entity.Property(e => e.Email)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("EMAIL");

                entity.Property(e => e.Estado)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("ESTADO");

                entity.Property(e => e.Fecharegistro)
                    .HasColumnType("datetime")
                    .HasColumnName("FECHAREGISTRO");

                entity.Property(e => e.Identificacion)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("IDENTIFICACION");

                entity.Property(e => e.Login)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("LOGIN");

                entity.Property(e => e.Nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("NOMBRE");

                entity.Property(e => e.Password)
                    .HasMaxLength(100)
                    .IsUnicode(false)
                    .HasColumnName("PASSWORD");

                entity.Property(e => e.Sede).HasColumnName("SEDE");

                entity.Property(e => e.Telefono)
                    .HasMaxLength(50)
                    .IsUnicode(false)
                    .HasColumnName("TELEFONO");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

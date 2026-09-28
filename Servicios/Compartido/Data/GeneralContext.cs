using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Compartido.Data.Modelos;

namespace Compartido.Data
{
    public partial class GeneralContext : DbContext
    {
        public GeneralContext()
        {
        }

        public GeneralContext(DbContextOptions<GeneralContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Apariencium> Apariencia { get; set; }
        public virtual DbSet<Empresa> Empresas { get; set; }
        public virtual DbSet<ParametrosGenerale> ParametrosGenerales { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Apariencium>(entity =>
            {
                entity.Property(e => e.colorCabecera)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.colorFondo)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.colorLetra)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.colorLetraCabecera)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.colorLetraPiePagina)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.colorPiePagina)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.tipoLetra)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.HasOne(d => d.empresaNavigation)
                    .WithMany(p => p.Apariencia)
                    .HasForeignKey(d => d.empresa)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Apariencia_Empresa");
            });

            modelBuilder.Entity<Empresa>(entity =>
            {
                entity.ToTable("Empresa");

                entity.Property(e => e.conexionBD)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .IsRequired()
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.logo)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.nit)
                    .IsRequired()
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.subDominio)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.telefono)
                    .HasMaxLength(15)
                    .IsUnicode(false);
            });

            modelBuilder.Entity<ParametrosGenerale>(entity =>
            {
                entity.Property(e => e.tipo)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.valor)
                    .IsRequired()
                    .HasMaxLength(100)
                    .IsUnicode(false);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

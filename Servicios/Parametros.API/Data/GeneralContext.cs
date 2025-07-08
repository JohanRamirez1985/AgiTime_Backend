using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Parametros.API.Data.Modelos;

namespace Parametros.API.Data
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

        public virtual DbSet<Empresa> Empresas { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Empresa>(entity =>
            {
                entity.ToTable("Empresa");

                entity.Property(e => e.conexionBD)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.estado)
                    .HasMaxLength(20)
                    .IsUnicode(false);

                entity.Property(e => e.logo)
                    .HasMaxLength(100)
                    .IsUnicode(false);

                entity.Property(e => e.nit)
                    .HasMaxLength(50)
                    .IsUnicode(false);

                entity.Property(e => e.nombre)
                    .HasMaxLength(100)
                    .IsUnicode(false);
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}

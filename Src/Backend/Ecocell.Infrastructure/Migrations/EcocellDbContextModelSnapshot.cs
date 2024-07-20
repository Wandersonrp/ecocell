﻿// <auto-generated />
using System;
using Ecocell.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ecocell.Infrastructure.Migrations
{
    [DbContext(typeof(EcocellDbContext))]
    partial class EcocellDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Ecocell.Domain.Entities.Address", b =>
                {
                    b.Property<Guid>("AddressId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("address_id");

                    b.Property<string>("City")
                        .IsRequired()
                        .HasColumnType("varchar(100)")
                        .HasColumnName("city");

                    b.Property<string>("Complement")
                        .HasColumnType("varchar(50)")
                        .HasColumnName("complement");

                    b.Property<string>("Country")
                        .IsRequired()
                        .HasColumnType("varchar(50)")
                        .HasColumnName("country");

                    b.Property<string>("Latitude")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Longitude")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Neighborhood")
                        .IsRequired()
                        .HasColumnType("varchar(50)")
                        .HasColumnName("neighborhood");

                    b.Property<string>("Number")
                        .IsRequired()
                        .HasColumnType("varchar(15)")
                        .HasColumnName("number");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasColumnType("varchar(50)")
                        .HasColumnName("state");

                    b.Property<string>("Street")
                        .IsRequired()
                        .HasColumnType("varchar(100)")
                        .HasColumnName("street");

                    b.Property<string>("ZipCode")
                        .IsRequired()
                        .HasColumnType("varchar(8)")
                        .HasColumnName("zip_code");

                    b.HasKey("AddressId")
                        .HasName("pk_address");

                    b.ToTable("Addresses", (string)null);
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.Document", b =>
                {
                    b.Property<Guid>("DocumentId")
                        .HasColumnType("uuid")
                        .HasColumnName("document_id");

                    b.Property<string>("DocumentType")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("document_type");

                    b.Property<string>("Text")
                        .IsRequired()
                        .HasColumnType("varchar(14)")
                        .HasColumnName("text");

                    b.HasKey("DocumentId")
                        .HasName("pk_document");

                    b.ToTable("Documents", (string)null);
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.User", b =>
                {
                    b.Property<Guid>("UserId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.Property<string>("AvatarUrl")
                        .HasColumnType("text")
                        .HasColumnName("avatar_url");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("created_at");

                    b.Property<int>("DocumentId")
                        .HasColumnType("integer")
                        .HasColumnName("document_id");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("varchar(100)")
                        .HasColumnName("email");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("varchar(100)")
                        .HasColumnName("name");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("password");

                    b.Property<string>("Role")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("role");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("status");

                    b.Property<DateTime?>("UpdatedAt")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("updated_at");

                    b.Property<string>("UserType")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("user_type");

                    b.HasKey("UserId");

                    b.ToTable("Users", (string)null);

                    b.UseTptMappingStrategy();
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.LegalPerson", b =>
                {
                    b.HasBaseType("Ecocell.Domain.Entities.User");

                    b.Property<Guid>("AddressId")
                        .HasColumnType("uuid")
                        .HasColumnName("address_id");

                    b.Property<string>("CompanyDescription")
                        .IsRequired()
                        .HasColumnType("varchar(200)")
                        .HasColumnName("company_description");

                    b.Property<DateOnly>("CompanyStartDate")
                        .HasColumnType("date")
                        .HasColumnName("company_start_date");

                    b.Property<string>("CompanyStatus")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("company_status");

                    b.Property<string>("CorporateName")
                        .IsRequired()
                        .HasColumnType("varchar(100)")
                        .HasColumnName("corporate_name");

                    b.Property<bool>("IsCollector")
                        .HasColumnType("boolean")
                        .HasColumnName("is_collector");

                    b.Property<bool>("IsDiscarding")
                        .HasColumnType("boolean")
                        .HasColumnName("is_discarding");

                    b.Property<bool>("IsDiscardingPoint")
                        .HasColumnType("boolean")
                        .HasColumnName("is_discarding_point");

                    b.Property<string>("Phone")
                        .IsRequired()
                        .HasColumnType("varchar(20)")
                        .HasColumnName("phone");

                    b.Property<string>("PrincipalCnae")
                        .IsRequired()
                        .HasColumnType("varchar(10)")
                        .HasColumnName("principal_cnae");

                    b.HasIndex("AddressId")
                        .IsUnique();

                    b.ToTable("LegalPeople", (string)null);
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.NaturalPerson", b =>
                {
                    b.HasBaseType("Ecocell.Domain.Entities.User");

                    b.Property<DateOnly?>("BirthDate")
                        .HasColumnType("date")
                        .HasColumnName("birth_date");

                    b.Property<bool>("IsDiscarding")
                        .HasColumnType("boolean")
                        .HasColumnName("is_discarding");

                    b.ToTable("NaturalPeople", (string)null);
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.Document", b =>
                {
                    b.HasOne("Ecocell.Domain.Entities.User", "User")
                        .WithOne("Document")
                        .HasForeignKey("Ecocell.Domain.Entities.Document", "DocumentId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.LegalPerson", b =>
                {
                    b.HasOne("Ecocell.Domain.Entities.Address", "Address")
                        .WithOne("LegalPerson")
                        .HasForeignKey("Ecocell.Domain.Entities.LegalPerson", "AddressId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Ecocell.Domain.Entities.User", null)
                        .WithOne()
                        .HasForeignKey("Ecocell.Domain.Entities.LegalPerson", "UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_legal_person_user");

                    b.Navigation("Address");
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.NaturalPerson", b =>
                {
                    b.HasOne("Ecocell.Domain.Entities.User", null)
                        .WithOne()
                        .HasForeignKey("Ecocell.Domain.Entities.NaturalPerson", "UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired()
                        .HasConstraintName("fk_natural_person_user");
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.Address", b =>
                {
                    b.Navigation("LegalPerson");
                });

            modelBuilder.Entity("Ecocell.Domain.Entities.User", b =>
                {
                    b.Navigation("Document")
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}

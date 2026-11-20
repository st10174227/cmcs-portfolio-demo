using CMCS_POE.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;

#nullable disable

namespace CMCS_POE.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20251027175911_MakeNavigationPropertyNullable")]
    partial class MakeNavigationPropertyNullable
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.10");

            modelBuilder.Entity("CMCS_POE.Models.Claim", b =>
                {
                    b.Property<int>("ClaimId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime?>("ApprovedDate")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreatedDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("DocumentName")
                        .HasColumnType("TEXT");

                    b.Property<string>("DocumentPath")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("HourlyRate")
                        .HasColumnType("TEXT");

                    b.Property<double>("HoursWorked")
                        .HasColumnType("REAL");

                    b.Property<bool>("IsPaid")
                        .HasColumnType("INTEGER");

                    b.Property<int>("LecturerId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LecturerName")
                        .HasColumnType("TEXT");

                    b.Property<string>("Month")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Notes")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("PaidDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("PaymentReference")
                        .HasColumnType("TEXT");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("ClaimId");

                    b.HasIndex("LecturerId");

                    b.ToTable("Claims");
                });

            modelBuilder.Entity("CMCS_POE.Models.Lecturer", b =>
                {
                    b.Property<int>("LecturerId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Department")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsActive")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<decimal>("StandardHourlyRate")
                        .HasColumnType("TEXT");

                    b.HasKey("LecturerId");

                    b.ToTable("Lecturers");

                    b.HasData(
                        new
                        {
                            LecturerId = 1,
                            Department = "Computer Science",
                            Email = "john.smith@university.ac.za",
                            FullName = "Dr. John Smith",
                            IsActive = true,
                            PhoneNumber = "021-555-0001",
                            StandardHourlyRate = 250m
                        },
                        new
                        {
                            LecturerId = 2,
                            Department = "Mathematics",
                            Email = "jane.doe@university.ac.za",
                            FullName = "Prof. Jane Doe",
                            IsActive = true,
                            PhoneNumber = "021-555-0002",
                            StandardHourlyRate = 300m
                        },
                        new
                        {
                            LecturerId = 3,
                            Department = "Engineering",
                            Email = "bob.wilson@university.ac.za",
                            FullName = "Dr. Bob Wilson",
                            IsActive = true,
                            PhoneNumber = "021-555-0003",
                            StandardHourlyRate = 280m
                        });
                });

            modelBuilder.Entity("CMCS_POE.Models.Claim", b =>
                {
                    b.HasOne("CMCS_POE.Models.Lecturer", "Lecturer")
                        .WithMany("Claims")
                        .HasForeignKey("LecturerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Lecturer");
                });

            modelBuilder.Entity("CMCS_POE.Models.Lecturer", b =>
                {
                    b.Navigation("Claims");
                });
#pragma warning restore 612, 618
        }
    }
}

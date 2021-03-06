﻿// <auto-generated />
using CalDav.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CalDav.Model.Migrations
{
    [DbContext(typeof(CalDavContext))]
    [Migration("20181222155313_init")]
    partial class init
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.0-rtm-35687");

            modelBuilder.Entity("CalDav.Models.CalDavAppointment", b =>
                {
                    b.Property<int>("CalendarId");

                    b.Property<string>("Href");

                    b.Property<string>("Etag");

                    b.Property<string>("LocalId");

                    b.HasKey("CalendarId", "Href");

                    b.ToTable("Appointments");
                });

            modelBuilder.Entity("CalDav.Models.CalDavCalendar", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Ctag");

                    b.Property<string>("Displayname");

                    b.Property<string>("Href");

                    b.Property<bool>("Initialized");

                    b.Property<string>("LocalId");

                    b.Property<int>("ServerId");

                    b.Property<bool>("ShouldSync");

                    b.Property<string>("SyncToken");

                    b.HasKey("Id");

                    b.HasIndex("ServerId");

                    b.ToTable("Calendars");
                });

            modelBuilder.Entity("CalDav.Models.CalDavServer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CalendarHomeSet");

                    b.Property<string>("Host");

                    b.Property<string>("Password");

                    b.Property<string>("UserDir");

                    b.Property<string>("Username");

                    b.HasKey("Id");

                    b.ToTable("Servers");
                });

            modelBuilder.Entity("CalDav.Models.CalDavAppointment", b =>
                {
                    b.HasOne("CalDav.Models.CalDavCalendar", "Calendar")
                        .WithMany("Appointments")
                        .HasForeignKey("CalendarId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("CalDav.Models.CalDavCalendar", b =>
                {
                    b.HasOne("CalDav.Models.CalDavServer", "Server")
                        .WithMany("Calendars")
                        .HasForeignKey("ServerId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}

﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Trader.Data;

namespace Trader.Data.Migrations
{
    [DbContext(typeof(TraderContext))]
    partial class TraderContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.4");

            modelBuilder.Entity("Trader.Data.OrderEntity", b =>
                {
                    b.Property<long>("OrderId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("ClientOrderId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<decimal>("CummulativeQuoteQuantity")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("ExecutedQuantity")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("IcebergQuantity")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsWorking")
                        .HasColumnType("INTEGER");

                    b.Property<long>("OrderListId")
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("OriginalQuantity")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("OriginalQuoteOrderQuantity")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("Price")
                        .HasColumnType("TEXT");

                    b.Property<int>("Side")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Status")
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("StopPrice")
                        .HasColumnType("TEXT");

                    b.Property<string>("Symbol")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Time")
                        .HasColumnType("TEXT");

                    b.Property<int>("TimeInForce")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("UpdateTime")
                        .HasColumnType("TEXT");

                    b.HasKey("OrderId");

                    b.HasIndex("Symbol", "OrderId");

                    b.ToTable("Orders");
                });

            modelBuilder.Entity("Trader.Data.TradeEntity", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("Commission")
                        .HasColumnType("TEXT");

                    b.Property<string>("CommissionAsset")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsBestMatch")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsBuyer")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsMaker")
                        .HasColumnType("INTEGER");

                    b.Property<long>("OrderId")
                        .HasColumnType("INTEGER");

                    b.Property<long>("OrderListId")
                        .HasColumnType("INTEGER");

                    b.Property<decimal>("Price")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("Quantity")
                        .HasColumnType("TEXT");

                    b.Property<decimal>("QuoteQuantity")
                        .HasColumnType("TEXT");

                    b.Property<string>("Symbol")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("Time")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Symbol", "Id");

                    b.HasIndex("Symbol", "OrderId");

                    b.ToTable("TradeEntity");
                });
#pragma warning restore 612, 618
        }
    }
}

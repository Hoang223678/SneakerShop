-- One database per microservice (database-per-service pattern).
-- Run once against your SQL Server instance (e.g. with sqlcmd or SSMS).
IF DB_ID(N'SneakerShop_Identity')  IS NULL CREATE DATABASE SneakerShop_Identity;
IF DB_ID(N'SneakerShop_Catalog')   IS NULL CREATE DATABASE SneakerShop_Catalog;
IF DB_ID(N'SneakerShop_Inventory') IS NULL CREATE DATABASE SneakerShop_Inventory;
IF DB_ID(N'SneakerShop_Ordering')  IS NULL CREATE DATABASE SneakerShop_Ordering;
GO

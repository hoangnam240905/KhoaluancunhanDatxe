/*
    Car Rental System - Database Setup
    Do an CNTT-KLCN108

    Chay script nay tren SQL Server (SSMS hoac sqlcmd).
    Thu tu: 01 -> 02 -> 03
*/

IF DB_ID(N'CarRentalDB') IS NOT NULL
BEGIN
    ALTER DATABASE CarRentalDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE CarRentalDB;
END
GO

CREATE DATABASE CarRentalDB
    COLLATE Vietnamese_CI_AS;
GO

USE CarRentalDB;
GO

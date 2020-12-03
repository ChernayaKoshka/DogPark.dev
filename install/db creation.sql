-- Queries as written in code require `lower_case_table_names` set if being run on a case-sensitive filesystem (like Linux)

CREATE DATABASE IF NOT EXISTS DogPark;
USE DogPark;

-- BEGIN .NET CORE IDENTITY

CREATE TABLE IF NOT EXISTS `user` (
    `IDUser` INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `UserName` VARCHAR(255) NOT NULL UNIQUE,
    `NormalizedUserName` VARCHAR(255) NOT NULL UNIQUE,
    `PasswordHash` VARCHAR(2048) NOT NULL
);

CREATE TABLE IF NOT EXISTS `role` (
    `IDRole` int NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `Name` VARCHAR(255) NOT NULL UNIQUE,
    `NormalizedName` VARCHAR(255) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS `userrole` (
    `IDUserRole` int NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `IDUser` int NOT NULL,
    `IDRole` int NOT NULL,
    CONSTRAINT `userrole_iduser_fk` FOREIGN KEY (`IDUser`) REFERENCES `User` (`IDUser`) ON DELETE CASCADE,
    CONSTRAINT `userrole_idrole_fk` FOREIGN KEY (`IDRole`) REFERENCES `Role` (`IDRole`) ON DELETE CASCADE
);

-- END .NET CORE IDENTITY

CREATE TABLE IF NOT EXISTS `article` (
    `IDArticle` int unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `IDUser` INT NOT NULL,
    `Created` DATETIME NOT NULL DEFAULT NOW(),
    `Modified` DATETIME NOT NULL DEFAULT NOW(),
    `Headline` VARCHAR(255) NOT NULL,
    `FilePath` VARCHAR(4096) NOT NULL,
    CONSTRAINT `article_iduser_fk` FOREIGN KEY (`IDUser`) REFERENCES `User` (`IDUser`)
);

CREATE TABLE IF NOT EXISTS `shorturl` (
    `IDShortUrl` int unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
    `IDUser` INT NOT NULL,
    `Short` VARCHAR(255) NOT NULL UNIQUE,
    `Long` VARCHAR(4096) NOT NULL UNIQUE,
    CONSTRAINT `shorturl_iduser_fk` FOREIGN KEY (`IDUser`) REFERENCES `User` (`IDUser`)
);
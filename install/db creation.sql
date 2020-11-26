-- Queries as written in code require `lower_case_table_names` set if being run on a case-sensitive filesystem (like Linux)

CREATE DATABASE DogPark;
USE DogPark;

CREATE TABLE `article` (
    `Article` int(11) unsigned NOT NULL AUTO_INCREMENT,
    `Headline` text NOT NULL,
    `Author` tinytext NOT NULL,
    `FilePath` text NOT NULL,
    PRIMARY KEY (`Article`)
);

CREATE TABLE `role` (
    `Role` int(11) NOT NULL AUTO_INCREMENT,
    `Name` tinytext NOT NULL,
    `NormalizedName` tinytext NOT NULL,
    PRIMARY KEY (`Role`),
    UNIQUE KEY `Role` (`Role`),
    FULLTEXT KEY `NormalizedName` (`NormalizedName`)
);

CREATE TABLE `shorturl` (
    `shorturl` int(11) unsigned NOT NULL AUTO_INCREMENT,
    `short` text NOT NULL,
    `long` text NOT NULL,
    PRIMARY KEY (`shorturl`),
    UNIQUE KEY `short` (`short`) USING HASH,
    UNIQUE KEY `long` (`long`) USING HASH
);

CREATE TABLE `user` (
    `User` int(11) NOT NULL AUTO_INCREMENT,
    `UserName` tinytext NOT NULL,
    `NormalizedUserName` text NOT NULL,
    `PasswordHash` text NOT NULL,
    PRIMARY KEY (`User`),
    UNIQUE KEY `User` (`User`),
    UNIQUE KEY `UserName` (`UserName`) USING HASH,
    FULLTEXT KEY `NormalizedUserName` (`NormalizedUserName`)
);

CREATE TABLE `user_role` (
    `UserRole` int(11) NOT NULL AUTO_INCREMENT,
    `User` int(11) NOT NULL,
    `Role` int(11) NOT NULL,
    PRIMARY KEY (`UserRole`),
    KEY `user` (`User`),
    KEY `role` (`Role`),
    CONSTRAINT `role_fk` FOREIGN KEY (`Role`) REFERENCES `role` (`Role`) ON DELETE CASCADE,
    CONSTRAINT `user_fk` FOREIGN KEY (`User`) REFERENCES `user` (`User`) ON DELETE CASCADE
);
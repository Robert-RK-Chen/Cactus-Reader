/*
 Navicat Premium Data Transfer

 Source Server         : MySQL-Localhost
 Source Server Type    : MySQL
 Source Server Version : 80043 (8.0.43)
 Source Host           : localhost:3306
 Source Schema         : cactus_reader

 Target Server Type    : MySQL
 Target Server Version : 80043 (8.0.43)
 File Encoding         : 65001

 Date: 27/08/2025 16:05:23
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for code
-- ----------------------------
DROP TABLE IF EXISTS `code`;
CREATE TABLE `code`  (
  `Email` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `VerifyCode` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `CreateTime` datetime(3) NOT NULL,
  `CodeType` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  PRIMARY KEY (`Email`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = gbk COLLATE = gbk_chinese_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for privatekey
-- ----------------------------
DROP TABLE IF EXISTS `privatekey`;
CREATE TABLE `privatekey`  (
  `UID` varchar(36) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `Key` text CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  PRIMARY KEY (`UID`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = gbk COLLATE = gbk_chinese_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for user
-- ----------------------------
DROP TABLE IF EXISTS `user`;
CREATE TABLE `user`  (
  `UID` varchar(36) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `Email` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `Name` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `Mobile` varchar(11) CHARACTER SET gbk COLLATE gbk_chinese_ci NULL DEFAULT NULL,
  `Password` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `RegistDate` datetime NOT NULL,
  PRIMARY KEY (`UID`) USING BTREE,
  INDEX `account`(`Email` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = gbk COLLATE = gbk_chinese_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for userkey
-- ----------------------------
DROP TABLE IF EXISTS `userkey`;
CREATE TABLE `userkey`  (
  `ID` varchar(36) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `UID` varchar(36) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `PublicKey` text CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `Attestation` text CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `DeviceID` varchar(36) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `LastLogonTime` datetime NOT NULL,
  PRIMARY KEY (`ID`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = gbk COLLATE = gbk_chinese_ci ROW_FORMAT = DYNAMIC;

SET FOREIGN_KEY_CHECKS = 1;

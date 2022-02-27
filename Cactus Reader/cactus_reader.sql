/*
 Navicat Premium Data Transfer

 Source Server         : MySQL-TDCloud
 Source Server Type    : MySQL
 Source Server Version : 80022
 Source Host           : sh-cdb-0q4l9dac.sql.tencentcdb.com:59121
 Source Schema         : cactus_reader

 Target Server Type    : MySQL
 Target Server Version : 80022
 File Encoding         : 65001

 Date: 27/02/2022 16:52:08
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for code
-- ----------------------------
DROP TABLE IF EXISTS `code`;
CREATE TABLE `code`  (
  `email` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `verify_code` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NULL DEFAULT NULL,
  `create_time` datetime(3) NOT NULL,
  PRIMARY KEY (`email`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = gbk COLLATE = gbk_chinese_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for user
-- ----------------------------
DROP TABLE IF EXISTS `user`;
CREATE TABLE `user`  (
  `uid` varchar(36) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `email` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `name` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `mobile` varchar(11) CHARACTER SET gbk COLLATE gbk_chinese_ci NULL DEFAULT NULL,
  `password` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  `profile` varchar(255) CHARACTER SET gbk COLLATE gbk_chinese_ci NOT NULL,
  PRIMARY KEY (`uid`) USING BTREE,
  INDEX `account`(`email`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = gbk COLLATE = gbk_chinese_ci ROW_FORMAT = Dynamic;

SET FOREIGN_KEY_CHECKS = 1;

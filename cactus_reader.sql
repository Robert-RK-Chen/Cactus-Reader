/*
 Cactus Reader 数据库结构（MySQL 8.0+）

 2026-08 重构要点：
   1. 字符集统一 utf8mb4（原 GBK 无法存储 emoji/生僻字，且与连接串/导出头不一致）
   2. code 表改为 (Email, CodeType) 复合主键：login/reset/register 三种验证码互不覆盖；
      验证码校验即删（防重放），限频与过期均由服务端逻辑处理
   3. 移除从未被使用的 privatekey 表（原 Key 字段为 MySQL 保留字）
   4. userkey 增加 (UID, DeviceID) 唯一索引（高频查询 WHERE UID=? AND DeviceID=?）
      及外键 ON DELETE CASCADE（删除用户级联清理设备密钥，杜绝孤儿数据）
   5. user.Email 改为 UNIQUE 索引（注册防重复的唯一性约束）
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for user
-- ----------------------------
DROP TABLE IF EXISTS `user`;
CREATE TABLE `user`  (
  `UID` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Email` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Mobile` varchar(11) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL DEFAULT NULL,
  `Password` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `RegistDate` datetime NOT NULL,
  PRIMARY KEY (`UID`) USING BTREE,
  UNIQUE INDEX `uk_email`(`Email`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for code
-- ----------------------------
DROP TABLE IF EXISTS `code`;
CREATE TABLE `code`  (
  `Email` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `CodeType` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'signin / reset / signup',
  `VerifyCode` varchar(16) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `CreateTime` datetime(3) NOT NULL,
  PRIMARY KEY (`Email`, `CodeType`) USING BTREE,
  INDEX `idx_create_time`(`CreateTime`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for userkey
-- ----------------------------
DROP TABLE IF EXISTS `userkey`;
CREATE TABLE `userkey`  (
  `ID` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `UID` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `PublicKey` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'RSA 公钥，Base64 编码',
  `Attestation` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NULL COMMENT '取证数据，Base64 编码',
  `DeviceID` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `LastSignInTime` datetime NOT NULL,
  PRIMARY KEY (`ID`) USING BTREE,
  UNIQUE INDEX `uk_uid_device`(`UID`, `DeviceID`) USING BTREE,
  CONSTRAINT `fk_userkey_user` FOREIGN KEY (`UID`) REFERENCES `user` (`UID`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci ROW_FORMAT = DYNAMIC;

-- ----------------------------
-- Table structure for vaultkey
-- 2026-08 新增：便签保险箱 —— 密码包裹的便签加密密钥（零知识存储）
-- ----------------------------
DROP TABLE IF EXISTS `vaultkey`;
CREATE TABLE `vaultkey`  (
  `UID` varchar(36) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
  `Salt` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'PBKDF2 盐，Base64 编码',
  `WrappedKey` text CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL COMMENT 'KEK（个人密码派生）加密后的便签密钥，Base64 编码',
  `UpdateTime` datetime NOT NULL,
  PRIMARY KEY (`UID`) USING BTREE,
  CONSTRAINT `fk_vaultkey_user` FOREIGN KEY (`UID`) REFERENCES `user` (`UID`) ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_unicode_ci ROW_FORMAT = DYNAMIC;

SET FOREIGN_KEY_CHECKS = 1;

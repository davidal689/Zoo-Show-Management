-- 1. Tạo Cơ Sở Dữ Liệu
CREATE DATABASE ZooShowMnmDb;
GO

USE ZooShowMnmDb;
GO

-- 2. Tạo Bảng Users (Tài khoản)
CREATE TABLE [Users] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Name] NVARCHAR(60) NOT NULL,
    [Username] NVARCHAR(50) NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    [Role] NVARCHAR(15) NOT NULL,
    [IsTemporaryPassword] BIT NOT NULL DEFAULT 0,
    [IsDeactivated] BIT NOT NULL DEFAULT 0
);
CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
GO

-- 3. Tạo Bảng Shows (Lịch diễn)
CREATE TABLE [Shows] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [Name] NVARCHAR(100) NOT NULL,
    [Description] NVARCHAR(500) NOT NULL,
    [DateTime] DATETIME2 NOT NULL,
    [Venue] NVARCHAR(100) NOT NULL,
    [SeatCapacity] INT NOT NULL,
    [RemainingSeatCapacity] INT NOT NULL,
    [TicketPrice] DECIMAL(18,2) NOT NULL,
    [Status] NVARCHAR(20) NOT NULL
);
GO

-- 4. Tạo Bảng Bookings (Đặt vé)
CREATE TABLE [Bookings] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [ReferenceNumber] NVARCHAR(10) NOT NULL,
    [ShowId] INT NOT NULL FOREIGN KEY REFERENCES [Shows]([Id]) ON DELETE CASCADE,
    [UserAccountId] INT NULL FOREIGN KEY REFERENCES [Users]([Id]) ON DELETE SET NULL,
    [WalkInVisitorName] NVARCHAR(60) NULL,
    [TicketQuantity] INT NOT NULL,
    [TotalPrice] DECIMAL(18,2) NOT NULL,
    [BookingDate] DATETIME2 NOT NULL,
    [BookingChannel] NVARCHAR(15) NOT NULL,
    [BookingStatus] NVARCHAR(15) NOT NULL
);
CREATE UNIQUE INDEX [IX_Bookings_ReferenceNumber] ON [Bookings] ([ReferenceNumber]);
GO

-- 5. Tạo Bảng SeatLocks (Khóa ghế tạm thời 10 phút)
CREATE TABLE [SeatLocks] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [ShowId] INT NOT NULL FOREIGN KEY REFERENCES [Shows]([Id]) ON DELETE CASCADE,
    [LockedBySession] NVARCHAR(100) NOT NULL,
    [TicketQuantity] INT NOT NULL,
    [ExpiresAt] DATETIME2 NOT NULL,
    [IsReleased] BIT NOT NULL DEFAULT 0
);
GO

-- 6. Tạo Bảng AuditLogs (Nhật ký bảo mật hệ thống)
CREATE TABLE [AuditLogs] (
    [Id] INT IDENTITY(1,1) PRIMARY KEY,
    [ActorAccountId] INT NULL FOREIGN KEY REFERENCES [Users]([Id]) ON DELETE SET NULL,
    [ActionType] NVARCHAR(50) NOT NULL,
    [TargetEntity] NVARCHAR(100) NOT NULL,
    [Timestamp] DATETIME2 NOT NULL
);
GO

-- ==============================================================
-- 7. NẠP DỮ LIỆU BAN ĐẦU (SEED DATA)
-- Mật khẩu mặc định tương ứng với đuôi 123 đã được mã hóa BCrypt
-- ==============================================================

-- Nạp tài khoản mặc định
INSERT INTO [Users] ([Name], [Username], [PasswordHash], [Role], [IsTemporaryPassword], [IsDeactivated])
VALUES 
-- admin123
(N'Admin Account', 'admin', '$2a$11$wK1Gq0X6W158jX4lT6j9kOKFh61w8pBsh1U55s7v.nK.oO4BvS34O', 'Administrator', 0, 0),
-- manager123
(N'Manager Account', 'manager', '$2a$11$tYh5/wK1Gq0X6W158jX4lTe19u3v4B5YtY8t4rBvL.a.oO4BvS34O', 'Show Manager', 0, 0),
-- cashier123
(N'Cashier Account', 'cashier', '$2a$11$lTe19u3v4B5YtY8t4rBvL.2a$11$wK1Gq0X6W158jX4lT6j9kO', 'Cashier', 0, 0),
-- visitor123
(N'Visitor Account', 'visitor', '$2a$11$wK1Gq0X6W158jX4lT6j9kOKFh61w8pBsh1U55s7v.nK.oO4BvS34O', 'Visitor', 0, 0);
GO

-- Nạp các show diễn mẫu
INSERT INTO [Shows] ([Name], [Description], [DateTime], [Venue], [SeatCapacity], [RemainingSeatCapacity], [TicketPrice], [Status])
VALUES 
(N'Dolphin & Seal Wonders', N'Xem các chú cá heo và hải cẩu thông minh nhào lộn tại rạp xiếc dưới nước.', DATEADD(day, 1, GETDATE()), N'Splash Amphitheater', 100, 100, 15.50, 'Published'),
(N'Wings of the Wild Bird Show', N'Show diễn của các loài chim săn mồi bay lượn biểu diễn kỹ năng.', DATEADD(day, 1, GETDATE()), N'Eagle Ridge Arena', 80, 80, 12.00, 'Published'),
(N'Majestic Lions Feed & Show', N'Gặp gỡ chúa sơn lâm và các màn trình diễn hoang dã cực kỳ hấp dẫn.', DATEADD(day, 2, GETDATE()), N'Savannah Lookout', 50, 50, 20.00, 'Published'),
(N'Secret Night Creatures (Draft)', N'Show diễn thử nghiệm các loài săn mồi ban đêm như dơi và cú.', DATEADD(day, 5, GETDATE()), N'Nocturnal Jungle Dome', 40, 40, 18.00, 'Draft');
GO
-- Đã bổ sung thêm cột SelectedSeats:
ALTER TABLE Bookings ADD SelectedSeats VARCHAR(255) NULL;
-- Đã bổ sung thêm cột SelectedSeats:
ALTER TABLE SeatLocks ADD SelectedSeats VARCHAR(255) NULL;

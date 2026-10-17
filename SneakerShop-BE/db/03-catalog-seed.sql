-- Demo data for SneakerShop_Catalog (same data the service seeds on first start).
USE SneakerShop_Catalog;
GO
SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Name = N'Nike')
    INSERT dbo.Brands (Name, Slug) VALUES (N'Nike', N'nike');
IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Name = N'Adidas')
    INSERT dbo.Brands (Name, Slug) VALUES (N'Adidas', N'adidas');
IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Name = N'New Balance')
    INSERT dbo.Brands (Name, Slug) VALUES (N'New Balance', N'new-balance');
IF NOT EXISTS (SELECT 1 FROM dbo.Brands WHERE Name = N'Puma')
    INSERT dbo.Brands (Name, Slug) VALUES (N'Puma', N'puma');

IF NOT EXISTS (SELECT 1 FROM dbo.Sneakers WHERE Id = '00000001-0000-0000-0000-000000000001')
    INSERT dbo.Sneakers (Id, Name, Slug, Description, BrandId, Gender, Price, ImageUrl, IsActive, CreatedAt)
    VALUES ('00000001-0000-0000-0000-000000000001', N'Nike Air Max 90', N'nike-air-max-90', N'Retro runner silhouette with a visible Air unit in the heel and a durable leather and mesh upper.',
            (SELECT Id FROM dbo.Brands WHERE Name = N'Nike'), N'Men', 129.99, N'https://placehold.co/600x400?text=Nike+Air+Max+90', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Sneakers WHERE Id = '00000001-0000-0000-0000-000000000002')
    INSERT dbo.Sneakers (Id, Name, Slug, Description, BrandId, Gender, Price, ImageUrl, IsActive, CreatedAt)
    VALUES ('00000001-0000-0000-0000-000000000002', N'Nike Dunk Low', N'nike-dunk-low', N'Court-inspired low-top with a clean two-tone leather upper and cupsole construction.',
            (SELECT Id FROM dbo.Brands WHERE Name = N'Nike'), N'Unisex', 119.99, N'https://placehold.co/600x400?text=Nike+Dunk+Low', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Sneakers WHERE Id = '00000001-0000-0000-0000-000000000003')
    INSERT dbo.Sneakers (Id, Name, Slug, Description, BrandId, Gender, Price, ImageUrl, IsActive, CreatedAt)
    VALUES ('00000001-0000-0000-0000-000000000003', N'Adidas Samba OG', N'adidas-samba-og', N'Terrace-style trainer with a soft leather upper, suede overlays and a gum rubber outsole.',
            (SELECT Id FROM dbo.Brands WHERE Name = N'Adidas'), N'Unisex', 109.99, N'https://placehold.co/600x400?text=Adidas+Samba+OG', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Sneakers WHERE Id = '00000001-0000-0000-0000-000000000004')
    INSERT dbo.Sneakers (Id, Name, Slug, Description, BrandId, Gender, Price, ImageUrl, IsActive, CreatedAt)
    VALUES ('00000001-0000-0000-0000-000000000004', N'Adidas Ultraboost Light', N'adidas-ultraboost-light', N'Lightweight running shoe with a responsive foam midsole and a sock-like knit upper.',
            (SELECT Id FROM dbo.Brands WHERE Name = N'Adidas'), N'Men', 189.99, N'https://placehold.co/600x400?text=Adidas+Ultraboost+Light', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Sneakers WHERE Id = '00000001-0000-0000-0000-000000000005')
    INSERT dbo.Sneakers (Id, Name, Slug, Description, BrandId, Gender, Price, ImageUrl, IsActive, CreatedAt)
    VALUES ('00000001-0000-0000-0000-000000000005', N'New Balance 550', N'new-balance-550', N'Basketball-heritage low-top with a leather upper and a cushioned, flat-profile sole.',
            (SELECT Id FROM dbo.Brands WHERE Name = N'New Balance'), N'Unisex', 129.99, N'https://placehold.co/600x400?text=New+Balance+550', 1, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM dbo.Sneakers WHERE Id = '00000001-0000-0000-0000-000000000006')
    INSERT dbo.Sneakers (Id, Name, Slug, Description, BrandId, Gender, Price, ImageUrl, IsActive, CreatedAt)
    VALUES ('00000001-0000-0000-0000-000000000006', N'Puma Suede Classic', N'puma-suede-classic', N'Iconic suede sneaker with a formstrip, padded collar and rubber outsole.',
            (SELECT Id FROM dbo.Brands WHERE Name = N'Puma'), N'Unisex', 79.99, N'https://placehold.co/600x400?text=Puma+Suede+Classic', 1, SYSUTCDATETIME());

IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000001')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000001', '00000001-0000-0000-0000-000000000001', N'NK-AM90-WHITE-BLACK-8.0', 8.0, N'White/Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000002')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000002', '00000001-0000-0000-0000-000000000001', N'NK-AM90-WHITE-BLACK-9.0', 9.0, N'White/Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000003')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000003', '00000001-0000-0000-0000-000000000001', N'NK-AM90-WHITE-BLACK-10.0', 10.0, N'White/Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000004')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000004', '00000001-0000-0000-0000-000000000001', N'NK-AM90-WHITE-BLACK-11.0', 11.0, N'White/Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000005')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000005', '00000001-0000-0000-0000-000000000002', N'NK-DNK-PANDA-8.0', 8.0, N'Panda');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000006')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000006', '00000001-0000-0000-0000-000000000002', N'NK-DNK-PANDA-9.0', 9.0, N'Panda');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000007')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000007', '00000001-0000-0000-0000-000000000002', N'NK-DNK-PANDA-10.0', 10.0, N'Panda');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000008')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000008', '00000001-0000-0000-0000-000000000002', N'NK-DNK-PANDA-11.0', 11.0, N'Panda');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000009')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000009', '00000001-0000-0000-0000-000000000003', N'AD-SMB-COREBLACK-8.0', 8.0, N'Core Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000010')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000010', '00000001-0000-0000-0000-000000000003', N'AD-SMB-COREBLACK-9.0', 9.0, N'Core Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000011')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000011', '00000001-0000-0000-0000-000000000003', N'AD-SMB-COREBLACK-10.0', 10.0, N'Core Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000012')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000012', '00000001-0000-0000-0000-000000000003', N'AD-SMB-COREBLACK-11.0', 11.0, N'Core Black');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000013')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000013', '00000001-0000-0000-0000-000000000004', N'AD-UBL-CLOUDWHITE-8.0', 8.0, N'Cloud White');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000014')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000014', '00000001-0000-0000-0000-000000000004', N'AD-UBL-CLOUDWHITE-9.0', 9.0, N'Cloud White');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000015')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000015', '00000001-0000-0000-0000-000000000004', N'AD-UBL-CLOUDWHITE-10.0', 10.0, N'Cloud White');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000016')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000016', '00000001-0000-0000-0000-000000000004', N'AD-UBL-CLOUDWHITE-11.0', 11.0, N'Cloud White');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000017')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000017', '00000001-0000-0000-0000-000000000005', N'NB-550-WHITE-GREEN-8.0', 8.0, N'White/Green');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000018')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000018', '00000001-0000-0000-0000-000000000005', N'NB-550-WHITE-GREEN-9.0', 9.0, N'White/Green');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000019')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000019', '00000001-0000-0000-0000-000000000005', N'NB-550-WHITE-GREEN-10.0', 10.0, N'White/Green');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000020')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000020', '00000001-0000-0000-0000-000000000005', N'NB-550-WHITE-GREEN-11.0', 11.0, N'White/Green');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000021')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000021', '00000001-0000-0000-0000-000000000006', N'PM-SUE-PEACOAT-8.0', 8.0, N'Peacoat');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000022')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000022', '00000001-0000-0000-0000-000000000006', N'PM-SUE-PEACOAT-9.0', 9.0, N'Peacoat');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000023')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000023', '00000001-0000-0000-0000-000000000006', N'PM-SUE-PEACOAT-10.0', 10.0, N'Peacoat');
IF NOT EXISTS (SELECT 1 FROM dbo.SneakerVariants WHERE Id = '00000002-0000-0000-0000-000000000024')
    INSERT dbo.SneakerVariants (Id, SneakerId, Sku, Size, Color) VALUES ('00000002-0000-0000-0000-000000000024', '00000001-0000-0000-0000-000000000006', N'PM-SUE-PEACOAT-11.0', 11.0, N'Peacoat');
GO

-- ============================================================
-- Seed data for open product training analytics tables
-- Run once to populate shoe data used by the Open Training section.
-- Safe to re-run: uses INSERT ... ON CONFLICT DO NOTHING
-- ============================================================

-- ──────────────────────────────────────────────────────────
-- 1.  amazon_shoe_products  (adds ~80 extra rows)
-- ──────────────────────────────────────────────────────────
INSERT INTO amazon_shoe_products
    ("Asin","Name","Brand","Category","Gender","Price","Currency","Rating","ReviewCount","ImageUrl","LastSynced","CreatedAt")
VALUES
-- Nike sneakers
('B001NKD9SM','Nike Air Max 270 Sneaker','Nike','sneakers','Men',130.00,'USD',4.6,18420,NULL,NOW(),NOW()),
('B002QYW8LQ','Nike Revolution 6 Running Shoe','Nike','sneakers','Women',65.00,'USD',4.4,9541,NULL,NOW(),NOW()),
('B003XMWXNQ','Nike Air Force 1 Low','Nike','sneakers','Unisex',90.00,'USD',4.7,32105,NULL,NOW(),NOW()),
('B004ZYWX1A','Nike Free RN 5.0','Nike','sneakers','Women',79.99,'USD',4.3,5820,NULL,NOW(),NOW()),
('B005AMXY2K','Nike Court Vision Low','Nike','sneakers','Men',65.00,'USD',4.5,12330,NULL,NOW(),NOW()),
('B006TRKL3P','Nike React Infinity Run Flyknit 3','Nike','sneakers','Men',160.00,'USD',4.6,7219,NULL,NOW(),NOW()),
-- Adidas sneakers
('B007KDOP4Q','Adidas Ultraboost 22','Adidas','sneakers','Men',190.00,'USD',4.6,14800,NULL,NOW(),NOW()),
('B008PVRS5R','Adidas Stan Smith','Adidas','sneakers','Unisex',80.00,'USD',4.5,22450,NULL,NOW(),NOW()),
('B009QWTS6S','Adidas Superstar Foundation','Adidas','sneakers','Unisex',75.00,'USD',4.4,18660,NULL,NOW(),NOW()),
('B010LMNU7T','Adidas Gazelle Indoor','Adidas','sneakers','Unisex',100.00,'USD',4.5,8340,NULL,NOW(),NOW()),
('B011ONPV8U','Adidas Grand Court 2.0','Adidas','sneakers','Men',60.00,'USD',4.3,6720,NULL,NOW(),NOW()),
('B012RQPW9V','Adidas Samba OG','Adidas','sneakers','Unisex',100.00,'USD',4.7,19870,NULL,NOW(),NOW()),
-- New Balance
('B013ST1X0W','New Balance Fresh Foam 1080v12','New Balance','sneakers','Men',164.99,'USD',4.7,9340,NULL,NOW(),NOW()),
('B014TUVY1X','New Balance 574 Classic','New Balance','sneakers','Unisex',89.99,'USD',4.6,15720,NULL,NOW(),NOW()),
('B015UVWZ2Y','New Balance 990v5 Made in USA','New Balance','sneakers','Men',184.99,'USD',4.8,7130,NULL,NOW(),NOW()),
('B016VWXA3Z','New Balance 327 Retro','New Balance','sneakers','Unisex',79.99,'USD',4.4,9820,NULL,NOW(),NOW()),
-- Puma
('B017WXYB4A','Puma RS-X³ Puzzle','Puma','sneakers','Unisex',110.00,'USD',4.3,6410,NULL,NOW(),NOW()),
('B018XYZC5B','Puma Suede Classic XXL','Puma','sneakers','Unisex',70.00,'USD',4.4,8930,NULL,NOW(),NOW()),
('B019YZAD6C','Puma Mayze Stack','Puma','sneakers','Women',85.00,'USD',4.2,4570,NULL,NOW(),NOW()),
-- Reebok
('B020ZABC7D','Reebok Classic Leather Legacy','Reebok','sneakers','Unisex',75.00,'USD',4.4,10280,NULL,NOW(),NOW()),
('B021ABCD8E','Reebok Nano X2 Training Shoe','Reebok','sneakers','Men',130.00,'USD',4.5,7640,NULL,NOW(),NOW()),
-- Converse
('B022BCDE9F','Converse Chuck Taylor All Star Hi','Converse','sneakers','Unisex',60.00,'USD',4.6,45210,NULL,NOW(),NOW()),
('B023CDEF0G','Converse Run Star Hike Platform','Converse','sneakers','Women',95.00,'USD',4.3,8120,NULL,NOW(),NOW()),
-- Vans
('B024DEFG1H','Vans Old Skool Platform','Vans','sneakers','Women',70.00,'USD',4.5,14390,NULL,NOW(),NOW()),
('B025EFGH2I','Vans Sk8-Hi Zip','Vans','sneakers','Men',80.00,'USD',4.4,9870,NULL,NOW(),NOW()),
-- Timberland boots
('B026FGHI3J','Timberland 6 Inch Premium Waterproof Boot','Timberland','boots','Men',198.00,'USD',4.7,28600,NULL,NOW(),NOW()),
('B027GHIJ4K','Timberland Chukka Boot','Timberland','boots','Men',150.00,'USD',4.5,9730,NULL,NOW(),NOW()),
('B028HIJK5L','Timberland White Ledge Mid Ankle Boot','Timberland','boots','Men',100.00,'USD',4.4,7450,NULL,NOW(),NOW()),
-- UGG boots
('B029IJKL6M','UGG Classic Short II Boot','UGG','boots','Women',170.00,'USD',4.6,19420,NULL,NOW(),NOW()),
('B030JKLM7N','UGG Neumel Chukka Boot','UGG','boots','Men',130.00,'USD',4.5,8780,NULL,NOW(),NOW()),
('B031KLMN8O','UGG Classic Mini II Boot','UGG','boots','Women',150.00,'USD',4.5,14320,NULL,NOW(),NOW()),
-- Dr. Martens
('B032LMNO9P','Dr. Martens 1460 8-Eye Boot','Dr. Martens','boots','Unisex',170.00,'USD',4.6,22130,NULL,NOW(),NOW()),
('B033MNOP0Q','Dr. Martens 1461 Oxford','Dr. Martens','oxfords','Unisex',140.00,'USD',4.5,11740,NULL,NOW(),NOW()),
-- Birkenstock sandals
('B034NOPQ1R','Birkenstock Arizona Soft Footbed','Birkenstock','sandals','Unisex',99.95,'USD',4.7,31450,NULL,NOW(),NOW()),
('B035OPQR2S','Birkenstock Gizeh Toe-Post Sandal','Birkenstock','sandals','Women',90.00,'USD',4.6,14780,NULL,NOW(),NOW()),
('B036PQRS3T','Birkenstock Boston Clog','Birkenstock','loafers','Unisex',110.00,'USD',4.7,18920,NULL,NOW(),NOW()),
-- Steve Madden heels
('B037QRST4U','Steve Madden Irenee Platform Sandal','Steve Madden','heels','Women',99.99,'USD',4.3,5640,NULL,NOW(),NOW()),
('B038RSTU5V','Steve Madden Carrson Block Heel','Steve Madden','heels','Women',89.99,'USD',4.2,4210,NULL,NOW(),NOW()),
-- Clarks
('B039STUV6W','Clarks Desert Boot Beeswax','Clarks','boots','Men',130.00,'USD',4.6,18730,NULL,NOW(),NOW()),
('B040TUVW7X','Clarks Wallabee Moccasin','Clarks','loafers','Men',130.00,'USD',4.5,12430,NULL,NOW(),NOW()),
-- Asics
('B041UVWX8Y','ASICS Gel-Nimbus 24','ASICS','sneakers','Men',160.00,'USD',4.6,9870,NULL,NOW(),NOW()),
('B042VWXY9Z','ASICS Gel-Kayano 28','ASICS','sneakers','Women',160.00,'USD',4.7,11230,NULL,NOW(),NOW()),
('B043WXYZ0A','ASICS Gel-Venture 8','ASICS','sneakers','Unisex',64.95,'USD',4.5,18740,NULL,NOW(),NOW()),
-- Skechers
('B044XYZA1B','Skechers GOwalk Joy Walking Shoe','Skechers','sneakers','Women',54.99,'USD',4.4,14320,NULL,NOW(),NOW()),
('B045YZAB2C','Skechers Arch Fit Memory Foam','Skechers','sneakers','Men',69.99,'USD',4.5,21870,NULL,NOW(),NOW()),
-- Crocs
('B046ZABC3D','Crocs Classic Clog','Crocs','sandals','Unisex',49.99,'USD',4.7,85400,NULL,NOW(),NOW()),
('B047ABCD4E','Crocs Bayaband Clog','Crocs','sandals','Unisex',44.99,'USD',4.5,19230,NULL,NOW(),NOW()),
-- Merrell
('B048BCDE5F','Merrell Moab 3 Hiking Shoe','Merrell','sneakers','Men',110.00,'USD',4.7,22450,NULL,NOW(),NOW()),
('B049CDEF6G','Merrell Jungle Moc Slip-On','Merrell','loafers','Women',80.00,'USD',4.5,9870,NULL,NOW(),NOW()),
-- Brooks
('B050DEFG7H','Brooks Ghost 14 Running Shoe','Brooks','sneakers','Men',140.00,'USD',4.7,18720,NULL,NOW(),NOW()),
('B051EFGH8I','Brooks Adrenaline GTS 22','Brooks','sneakers','Women',130.00,'USD',4.6,14380,NULL,NOW(),NOW()),
-- Hoka
('B052FGHI9J','HOKA Clifton 8 Running Shoe','HOKA','sneakers','Men',130.00,'USD',4.7,14210,NULL,NOW(),NOW()),
('B053GHIJ0K','HOKA Bondi 7','HOKA','sneakers','Women',165.00,'USD',4.6,9430,NULL,NOW(),NOW()),
('B054HIJK1L','HOKA Anacapa Low GORE-TEX Hiking Shoe','HOKA','sneakers','Unisex',175.00,'USD',4.5,4820,NULL,NOW(),NOW()),
-- On Running
('B055IJKL2M','On Cloud 5 Running Shoe','On','sneakers','Men',139.99,'USD',4.5,11450,NULL,NOW(),NOW()),
('B056JKLM3N','On Cloudmonster','On','sneakers','Women',169.99,'USD',4.6,6870,NULL,NOW(),NOW()),
-- Cole Haan
('B057KLMN4O','Cole Haan ZeroGrand Wingtip Oxford','Cole Haan','oxfords','Men',170.00,'USD',4.5,8320,NULL,NOW(),NOW()),
('B058LMNO5P','Cole Haan Grand Crosscourt II','Cole Haan','sneakers','Men',100.00,'USD',4.4,9740,NULL,NOW(),NOW()),
-- Salomon
('B059MNOP6Q','Salomon Speedcross 5 Trail Running Shoe','Salomon','sneakers','Men',130.00,'USD',4.7,18430,NULL,NOW(),NOW()),
('B060NOPQ7R','Salomon X Ultra 4 Mid GTX Hiking Boot','Salomon','boots','Men',175.00,'USD',4.6,9120,NULL,NOW(),NOW()),
-- Ecco
('B061OPQR8S','ECCO Soft 7 Low Top Sneaker','ECCO','sneakers','Men',130.00,'USD',4.5,6780,NULL,NOW(),NOW()),
('B062PQRS9T','ECCO Bella Ballet Flat','ECCO','sandals','Women',99.99,'USD',4.4,4310,NULL,NOW(),NOW()),
-- Keen
('B063QRST0U','KEEN Targhee III Waterproof Hiking Shoe','Keen','sneakers','Men',120.00,'USD',4.6,15420,NULL,NOW(),NOW()),
('B064RSTU1V','KEEN Newport H2 Water Sandal','Keen','sandals','Unisex',84.95,'USD',4.6,18750,NULL,NOW(),NOW()),
-- Wolverine
('B065STUV2W','Wolverine Floorhand 6" Steel-Toe Work Boot','Wolverine','boots','Men',109.99,'USD',4.4,7210,NULL,NOW(),NOW()),
-- Tommy Hilfiger
('B066TUVW3X','Tommy Hilfiger Listo Sneaker','Tommy Hilfiger','sneakers','Men',75.00,'USD',4.2,6340,NULL,NOW(),NOW()),
('B067UVWX4Y','Tommy Hilfiger Elaine Platform Sandal','Tommy Hilfiger','heels','Women',79.95,'USD',4.1,3850,NULL,NOW(),NOW()),
-- Michael Kors
('B068VWXY5Z','Michael Kors Colby Slip-On Sneaker','Michael Kors','sneakers','Women',98.00,'USD',4.2,5120,NULL,NOW(),NOW()),
('B069WXYZ6A','Michael Kors Simone Platform Sandal','Michael Kors','heels','Women',110.00,'USD',4.1,3460,NULL,NOW(),NOW()),
-- Guess
('B070XYZA7B','GUESS Vibo Lace-Up Sneaker','Guess','sneakers','Men',79.00,'USD',4.0,2740,NULL,NOW(),NOW()),
-- Naturalizer
('B071YZAB8C','Naturalizer Vera Slip-On Loafer','Naturalizer','loafers','Women',75.00,'USD',4.4,6840,NULL,NOW(),NOW()),
('B072ZABC9D','Naturalizer Jessie Platform Pump','Naturalizer','heels','Women',99.00,'USD',4.3,4230,NULL,NOW(),NOW()),
-- Sam Edelman
('B073ABCD0E','Sam Edelman Circus Bailey Heeled Sandal','Sam Edelman','heels','Women',79.95,'USD',4.3,7810,NULL,NOW(),NOW()),
('B074BCDE1F','Sam Edelman Felicia Ballet Flat','Sam Edelman','sandals','Women',69.95,'USD',4.4,9740,NULL,NOW(),NOW()),
-- Sorel
('B075CDEF2G','SOREL Caribou Waterproof Boot','SOREL','boots','Women',179.95,'USD',4.6,11230,NULL,NOW(),NOW()),
('B076DEFG3H','SOREL Joan of Arctic Wedge Boot','SOREL','boots','Women',199.95,'USD',4.5,8910,NULL,NOW(),NOW()),
-- Under Armour
('B077EFGH4I','Under Armour Charged Assert 9','Under Armour','sneakers','Men',64.99,'USD',4.5,18400,NULL,NOW(),NOW()),
('B078FGHI5J','Under Armour HOVR Phantom 2 Running Shoe','Under Armour','sneakers','Women',130.00,'USD',4.4,7230,NULL,NOW(),NOW())
ON CONFLICT ("Asin") DO NOTHING;


-- ──────────────────────────────────────────────────────────
-- 2.  ebay_shoe_products  (inserts ~100 rows)
-- ──────────────────────────────────────────────────────────
INSERT INTO ebay_shoe_products
    ("EbayItemId","Name","Brand","Category","Gender","Price","Currency","Rating","ReviewCount","ImageUrl","LastSynced","CreatedAt")
VALUES
-- Nike
('EB1001','Nike Air Max 90 White/Black Sneaker','Nike','sneakers','Men',89.99,'USD',4.6,14320,NULL,NOW(),NOW()),
('EB1002','Nike Air Jordan 1 Retro High OG','Nike','sneakers','Men',170.00,'USD',4.8,28450,NULL,NOW(),NOW()),
('EB1003','Nike Air Jordan 4 Retro Bred','Nike','sneakers','Men',210.00,'USD',4.8,19830,NULL,NOW(),NOW()),
('EB1004','Nike Dunk Low Panda','Nike','sneakers','Unisex',110.00,'USD',4.7,22140,NULL,NOW(),NOW()),
('EB1005','Nike Air Zoom Pegasus 39','Nike','sneakers','Women',130.00,'USD',4.6,11820,NULL,NOW(),NOW()),
('EB1006','Nike Blazer Mid 77 Vintage','Nike','sneakers','Unisex',100.00,'USD',4.5,9740,NULL,NOW(),NOW()),
('EB1007','Nike Air Huarache Run','Nike','sneakers','Men',115.00,'USD',4.5,8430,NULL,NOW(),NOW()),
('EB1008','Nike Metcon 7 Training Shoe','Nike','sneakers','Men',130.00,'USD',4.5,6890,NULL,NOW(),NOW()),
-- Adidas
('EB1009','Adidas Yeezy Boost 350 V2 Zebra','Adidas','sneakers','Unisex',240.00,'USD',4.6,17320,NULL,NOW(),NOW()),
('EB1010','Adidas NMD R1 Core Black','Adidas','sneakers','Men',130.00,'USD',4.5,13870,NULL,NOW(),NOW()),
('EB1011','Adidas Forum Low Cloud White','Adidas','sneakers','Unisex',90.00,'USD',4.4,8950,NULL,NOW(),NOW()),
('EB1012','Adidas Terrex Free Hiker Shoe','Adidas','sneakers','Men',200.00,'USD',4.6,7230,NULL,NOW(),NOW()),
('EB1013','Adidas Alphaboost V1','Adidas','sneakers','Women',100.00,'USD',4.3,6320,NULL,NOW(),NOW()),
('EB1014','Adidas Response CL','Adidas','sneakers','Unisex',65.00,'USD',4.2,4870,NULL,NOW(),NOW()),
('EB1015','Adidas Handball Spezial Brown','Adidas','sneakers','Unisex',100.00,'USD',4.6,14210,NULL,NOW(),NOW()),
-- New Balance
('EB1016','New Balance 2002R Protection Pack','New Balance','sneakers','Unisex',150.00,'USD',4.7,12450,NULL,NOW(),NOW()),
('EB1017','New Balance 550 Varsity Green','New Balance','sneakers','Unisex',100.00,'USD',4.6,9870,NULL,NOW(),NOW()),
('EB1018','New Balance 996 v4','New Balance','sneakers','Women',89.99,'USD',4.5,7340,NULL,NOW(),NOW()),
('EB1019','New Balance Fresh Foam X 860v12','New Balance','sneakers','Men',139.99,'USD',4.6,6120,NULL,NOW(),NOW()),
-- Jordan Brand
('EB1020','Air Jordan 3 Retro Fire Red','Jordan','sneakers','Men',200.00,'USD',4.8,17430,NULL,NOW(),NOW()),
('EB1021','Air Jordan 11 Retro Low Concord','Jordan','sneakers','Men',185.00,'USD',4.8,21870,NULL,NOW(),NOW()),
('EB1022','Air Jordan 6 Retro Carmine','Jordan','sneakers','Men',195.00,'USD',4.7,14320,NULL,NOW(),NOW()),
('EB1023','Air Jordan 12 Retro Taxi','Jordan','sneakers','Men',190.00,'USD',4.7,12780,NULL,NOW(),NOW()),
-- Puma
('EB1024','Puma Clyde All-Pro Basketball Shoe','Puma','sneakers','Men',110.00,'USD',4.4,5640,NULL,NOW(),NOW()),
('EB1025','Puma Speedcat OG','Puma','sneakers','Unisex',75.00,'USD',4.4,9320,NULL,NOW(),NOW()),
('EB1026','Puma Palermo Leather','Puma','sneakers','Unisex',80.00,'USD',4.5,7840,NULL,NOW(),NOW()),
('EB1027','Puma Future Rider Twofold','Puma','sneakers','Women',65.00,'USD',4.1,3870,NULL,NOW(),NOW()),
-- Reebok
('EB1028','Reebok Club C 85 Vintage','Reebok','sneakers','Unisex',75.00,'USD',4.5,11340,NULL,NOW(),NOW()),
('EB1029','Reebok Answer IV Iverson','Reebok','sneakers','Men',120.00,'USD',4.5,7640,NULL,NOW(),NOW()),
('EB1030','Reebok Instapump Fury 95','Reebok','sneakers','Unisex',150.00,'USD',4.3,5230,NULL,NOW(),NOW()),
-- Converse
('EB1031','Converse Chuck 70 High Top','Converse','sneakers','Unisex',85.00,'USD',4.5,18740,NULL,NOW(),NOW()),
('EB1032','Converse One Star Platform','Converse','sneakers','Women',70.00,'USD',4.3,7430,NULL,NOW(),NOW()),
('EB1033','Converse Chuck Taylor 70 Hi Parchment','Converse','sneakers','Unisex',90.00,'USD',4.4,9870,NULL,NOW(),NOW()),
-- Vans
('EB1034','Vans Authentic White/White','Vans','sneakers','Unisex',55.00,'USD',4.6,21430,NULL,NOW(),NOW()),
('EB1035','Vans Era 59 Canvas','Vans','sneakers','Men',70.00,'USD',4.5,12840,NULL,NOW(),NOW()),
('EB1036','Vans Half Cab 33 DX','Vans','sneakers','Unisex',100.00,'USD',4.4,6790,NULL,NOW(),NOW()),
-- Timberland boots
('EB1037','Timberland Euro Sprint Hiker Boot','Timberland','boots','Men',165.00,'USD',4.5,8640,NULL,NOW(),NOW()),
('EB1038','Timberland Brooklyn Side-Zip Chelsea Boot','Timberland','boots','Women',150.00,'USD',4.4,6320,NULL,NOW(),NOW()),
('EB1039','Timberland MT Maddsen Mid Waterproof Boot','Timberland','boots','Men',145.00,'USD',4.5,7180,NULL,NOW(),NOW()),
-- UGG
('EB1040','UGG Tasman Slipper Chestnut','UGG','slippers','Unisex',110.00,'USD',4.7,24780,NULL,NOW(),NOW()),
('EB1041','UGG Fluff Yeah Slide','UGG','slippers','Women',95.00,'USD',4.6,18450,NULL,NOW(),NOW()),
('EB1042','UGG Classic Ultra Mini Boot','UGG','boots','Women',160.00,'USD',4.5,13240,NULL,NOW(),NOW()),
-- Dr. Martens
('EB1043','Dr. Martens Jadon Platform Boot','Dr. Martens','boots','Unisex',200.00,'USD',4.6,14870,NULL,NOW(),NOW()),
('EB1044','Dr. Martens Audrick Chelsea Boot','Dr. Martens','boots','Women',190.00,'USD',4.4,7320,NULL,NOW(),NOW()),
('EB1045','Dr. Martens 2976 Yellow Stitch Chelsea Boot','Dr. Martens','boots','Unisex',170.00,'USD',4.5,11430,NULL,NOW(),NOW()),
-- Birkenstock
('EB1046','Birkenstock Madrid Big Buckle','Birkenstock','sandals','Women',110.00,'USD',4.6,12840,NULL,NOW(),NOW()),
('EB1047','Birkenstock Tatami Cork Sandal','Birkenstock','sandals','Unisex',95.00,'USD',4.5,8760,NULL,NOW(),NOW()),
('EB1048','Birkenstock Papillio Eve Platform','Birkenstock','heels','Women',100.00,'USD',4.4,5420,NULL,NOW(),NOW()),
-- Steve Madden
('EB1049','Steve Madden Troopa Combat Boot','Steve Madden','boots','Women',99.95,'USD',4.3,8740,NULL,NOW(),NOW()),
('EB1050','Steve Madden Bristan Lug Sole Boot','Steve Madden','boots','Women',119.95,'USD',4.2,5340,NULL,NOW(),NOW()),
('EB1051','Steve Madden Smcolsen Oxford','Steve Madden','oxfords','Men',89.95,'USD',4.1,3120,NULL,NOW(),NOW()),
-- ASICS
('EB1052','ASICS GT-2000 11 Running Shoe','ASICS','sneakers','Men',100.00,'USD',4.5,13240,NULL,NOW(),NOW()),
('EB1053','ASICS Gel-Cumulus 24','ASICS','sneakers','Women',130.00,'USD',4.6,9870,NULL,NOW(),NOW()),
('EB1054','ASICS Gel-DS Trainer 26','ASICS','sneakers','Men',120.00,'USD',4.5,7230,NULL,NOW(),NOW()),
-- Skechers
('EB1055','Skechers D''Lites Chunky Sneaker','Skechers','sneakers','Women',79.99,'USD',4.4,17430,NULL,NOW(),NOW()),
('EB1056','Skechers Stamina Airy Running Shoe','Skechers','sneakers','Men',64.99,'USD',4.3,9840,NULL,NOW(),NOW()),
('EB1057','Skechers Relaxed Fit Expected 2.0','Skechers','loafers','Men',62.99,'USD',4.4,11230,NULL,NOW(),NOW()),
-- Crocs
('EB1058','Crocs Classic All Terrain Clog','Crocs','sandals','Unisex',59.99,'USD',4.5,21340,NULL,NOW(),NOW()),
('EB1059','Crocs Brooklyn Low Wedge','Crocs','heels','Women',54.99,'USD',4.4,9870,NULL,NOW(),NOW()),
('EB1060','Crocs LiteRide 360 Pacer Sneaker','Crocs','sneakers','Women',84.99,'USD',4.3,7234,NULL,NOW(),NOW()),
-- Hoka
('EB1061','HOKA Rincon 3 Running Shoe','HOKA','sneakers','Women',130.00,'USD',4.6,12450,NULL,NOW(),NOW()),
('EB1062','HOKA Speedgoat 5 Trail Shoe','HOKA','sneakers','Men',145.00,'USD',4.6,9340,NULL,NOW(),NOW()),
('EB1063','HOKA Transport Sneaker','HOKA','sneakers','Unisex',130.00,'USD',4.4,7120,NULL,NOW(),NOW()),
-- On Running
('EB1064','On Cloud X 3 Training Shoe','On','sneakers','Women',160.00,'USD',4.5,8750,NULL,NOW(),NOW()),
('EB1065','On Cloudnova Form','On','sneakers','Men',149.99,'USD',4.4,6320,NULL,NOW(),NOW()),
-- Brooks
('EB1066','Brooks Glycerin 20 Running Shoe','Brooks','sneakers','Women',159.99,'USD',4.7,11240,NULL,NOW(),NOW()),
('EB1067','Brooks Levitate StealthFit 6','Brooks','sneakers','Men',149.99,'USD',4.5,7340,NULL,NOW(),NOW()),
-- Clarks
('EB1068','Clarks Originals Wallabee Boot','Clarks','boots','Men',150.00,'USD',4.6,13870,NULL,NOW(),NOW()),
('EB1069','Clarks Un Adorn Lace Oxford','Clarks','oxfords','Women',89.99,'USD',4.4,6420,NULL,NOW(),NOW()),
('EB1070','Clarks Tilden Cap Oxford','Clarks','oxfords','Men',79.99,'USD',4.5,9870,NULL,NOW(),NOW()),
-- Merrell
('EB1071','Merrell Choprock Trail Shoe','Merrell','sneakers','Men',110.00,'USD',4.5,8430,NULL,NOW(),NOW()),
('EB1072','Merrell Siren 3 GTX Hiking Shoe','Merrell','sneakers','Women',130.00,'USD',4.6,7840,NULL,NOW(),NOW()),
-- Salomon
('EB1073','Salomon XT-6 Advanced Trail Shoe','Salomon','sneakers','Men',140.00,'USD',4.6,11230,NULL,NOW(),NOW()),
('EB1074','Salomon Alphacross 3 Trail Shoe','Salomon','sneakers','Women',110.00,'USD',4.5,7870,NULL,NOW(),NOW()),
-- Cole Haan
('EB1075','Cole Haan Grand Ambition Double Gore Slip-On','Cole Haan','sneakers','Women',130.00,'USD',4.4,6780,NULL,NOW(),NOW()),
('EB1076','Cole Haan Pinch Grand Classic Penny Loafer','Cole Haan','loafers','Men',200.00,'USD',4.5,9230,NULL,NOW(),NOW()),
-- Ecco
('EB1077','ECCO MX Low Sneaker','ECCO','sneakers','Men',180.00,'USD',4.5,5340,NULL,NOW(),NOW()),
('EB1078','ECCO Biom 2.0 Low Sneaker','ECCO','sneakers','Women',160.00,'USD',4.4,4870,NULL,NOW(),NOW()),
-- Naturalizer
('EB1079','Naturalizer Rosette Slip-On Sneaker','Naturalizer','sneakers','Women',79.99,'USD',4.3,6740,NULL,NOW(),NOW()),
('EB1080','Naturalizer Boldon Pump','Naturalizer','heels','Women',89.99,'USD',4.4,5320,NULL,NOW(),NOW()),
-- Sam Edelman
('EB1081','Sam Edelman Yaro Ankle Boot','Sam Edelman','boots','Women',129.95,'USD',4.4,8740,NULL,NOW(),NOW()),
('EB1082','Sam Edelman Gigi Kitten Heel','Sam Edelman','heels','Women',109.95,'USD',4.3,5430,NULL,NOW(),NOW()),
-- SOREL
('EB1083','SOREL Kinetic Caribou X Chelsea Boot','SOREL','boots','Women',200.00,'USD',4.5,6430,NULL,NOW(),NOW()),
('EB1084','SOREL Out N About III Classic Boot','SOREL','boots','Women',165.00,'USD',4.6,9870,NULL,NOW(),NOW()),
-- Under Armour
('EB1085','Under Armour Charged Pursuit 3 Big Logo','Under Armour','sneakers','Men',59.99,'USD',4.4,14320,NULL,NOW(),NOW()),
('EB1086','Under Armour HOVR Sonic 5 Running Shoe','Under Armour','sneakers','Women',100.00,'USD',4.5,9840,NULL,NOW(),NOW()),
-- Wolverine
('EB1087','Wolverine Overpass 6" Composite Toe Boot','Wolverine','boots','Men',134.99,'USD',4.4,7230,NULL,NOW(),NOW()),
('EB1088','Wolverine Hellcat UltraSpring 6" Work Boot','Wolverine','boots','Men',159.99,'USD',4.5,6120,NULL,NOW(),NOW()),
-- Red Wing
('EB1089','Red Wing Iron Ranger 6" Boot Amber Harness','Red Wing','boots','Men',349.99,'USD',4.8,9870,NULL,NOW(),NOW()),
('EB1090','Red Wing Heritage Classic Moc Toe Boot','Red Wing','boots','Men',309.99,'USD',4.7,7640,NULL,NOW(),NOW()),
-- Keen
('EB1091','KEEN Jasper Climbing Shoe','Keen','sneakers','Men',100.00,'USD',4.4,7120,NULL,NOW(),NOW()),
('EB1092','KEEN Whisper Sandal','Keen','sandals','Women',74.95,'USD',4.5,12430,NULL,NOW(),NOW()),
-- Teva
('EB1093','Teva Original Universal Sandal','Teva','sandals','Unisex',55.00,'USD',4.5,18740,NULL,NOW(),NOW()),
('EB1094','Teva Hurricane XLT2 Sandal','Teva','sandals','Men',65.00,'USD',4.4,14320,NULL,NOW(),NOW()),
('EB1095','Teva Terra Fi 5 Universal Sandal','Teva','sandals','Women',95.00,'USD',4.5,9870,NULL,NOW(),NOW()),
-- Chaco
('EB1096','Chaco Z/1 Classic Sandal','Chaco','sandals','Men',100.00,'USD',4.6,16430,NULL,NOW(),NOW()),
('EB1097','Chaco ZX/2 Classic Sandal','Chaco','sandals','Women',105.00,'USD',4.6,12870,NULL,NOW(),NOW()),
-- OluKai
('EB1098','OluKai Ohana Slipper','OluKai','sandals','Men',65.00,'USD',4.6,11240,NULL,NOW(),NOW()),
('EB1099','OluKai Nohea Moku Slip-On','OluKai','loafers','Women',95.00,'USD',4.5,8340,NULL,NOW(),NOW()),
('EB1100','OluKai Pehuea Li Slip-On Sneaker','OluKai','sneakers','Women',99.95,'USD',4.5,6780,NULL,NOW(),NOW())
ON CONFLICT ("EbayItemId") DO NOTHING;


-- ──────────────────────────────────────────────────────────
-- 3.  google_shopping_products  (adds ~40 extra rows)
--     Position = search rank (lower = more prominent)
-- ──────────────────────────────────────────────────────────
INSERT INTO google_shopping_products
    ("ProductId","Title","Brand","Category","Gender","Price","Currency","Rating","ReviewCount","Position","ImageUrl","LastSynced","CreatedAt")
VALUES
('G001','Nike Air Max 270 React ENG','Nike','sneakers','Men',150.00,'USD',4.7,0,1,NULL,NOW(),NOW()),
('G002','Adidas Yeezy Slide Pure','Adidas','sandals','Unisex',70.00,'USD',4.6,0,2,NULL,NOW(),NOW()),
('G003','New Balance 9060 Triple Black','New Balance','sneakers','Unisex',150.00,'USD',4.7,0,3,NULL,NOW(),NOW()),
('G004','Nike Dunk High Retro White/Black','Nike','sneakers','Men',110.00,'USD',4.7,0,4,NULL,NOW(),NOW()),
('G005','Adidas Campus 00s White/Green','Adidas','sneakers','Unisex',100.00,'USD',4.6,0,5,NULL,NOW(),NOW()),
('G006','Jordan 1 Low OG Black/White','Jordan','sneakers','Unisex',100.00,'USD',4.7,0,6,NULL,NOW(),NOW()),
('G007','New Balance 1906R Protection Pack','New Balance','sneakers','Unisex',175.00,'USD',4.7,0,7,NULL,NOW(),NOW()),
('G008','UGG Classic Short Chestnut','UGG','boots','Women',175.00,'USD',4.8,0,8,NULL,NOW(),NOW()),
('G009','Dr. Martens Smooth 1460 Boot','Dr. Martens','boots','Unisex',180.00,'USD',4.6,0,9,NULL,NOW(),NOW()),
('G010','Birkenstock Boston Soft Footbed Suede','Birkenstock','loafers','Unisex',125.00,'USD',4.7,0,10,NULL,NOW(),NOW()),
('G011','HOKA Mach 5 Running Shoe','HOKA','sneakers','Men',140.00,'USD',4.6,0,11,NULL,NOW(),NOW()),
('G012','On Cloudflow 4','On','sneakers','Women',149.99,'USD',4.5,0,12,NULL,NOW(),NOW()),
('G013','Brooks Hyperion Max','Brooks','sneakers','Men',200.00,'USD',4.6,0,13,NULL,NOW(),NOW()),
('G014','Salomon ACS Pro Advanced','Salomon','sneakers','Unisex',200.00,'USD',4.5,0,14,NULL,NOW(),NOW()),
('G015','Nike Vaporfly 3','Nike','sneakers','Men',250.00,'USD',4.7,0,15,NULL,NOW(),NOW()),
('G016','Adidas Adizero Adios Pro 3','Adidas','sneakers','Women',250.00,'USD',4.6,0,16,NULL,NOW(),NOW()),
('G017','New Balance FuelCell SuperComp Elite v3','New Balance','sneakers','Men',260.00,'USD',4.5,0,17,NULL,NOW(),NOW()),
('G018','ASICS Metaspeed Sky+','ASICS','sneakers','Women',300.00,'USD',4.6,0,18,NULL,NOW(),NOW()),
('G019','Timberland Waterproof Chukka 6"','Timberland','boots','Men',160.00,'USD',4.5,0,19,NULL,NOW(),NOW()),
('G020','Steve Madden Mina Strappy Heel','Steve Madden','heels','Women',79.95,'USD',4.2,0,20,NULL,NOW(),NOW()),
('G021','Sam Edelman Hazel Ankle Strap Heel','Sam Edelman','heels','Women',89.95,'USD',4.3,0,21,NULL,NOW(),NOW()),
('G022','Cole Haan ZERØGRAND Overtake Runner 2','Cole Haan','sneakers','Men',130.00,'USD',4.4,0,22,NULL,NOW(),NOW()),
('G023','Merrell Wildwood Aerosport Hiking Shoe','Merrell','sneakers','Women',130.00,'USD',4.5,0,23,NULL,NOW(),NOW()),
('G024','Keen WK400 Walking Shoe','Keen','sneakers','Men',110.00,'USD',4.5,0,24,NULL,NOW(),NOW()),
('G025','Crocs Echo Clog','Crocs','sandals','Unisex',64.99,'USD',4.4,0,25,NULL,NOW(),NOW()),
('G026','Naturalizer Whitney Pump','Naturalizer','heels','Women',99.00,'USD',4.3,0,26,NULL,NOW(),NOW()),
('G027','Tommy Hilfiger Jaelene Chunky Sneaker','Tommy Hilfiger','sneakers','Women',79.95,'USD',4.1,0,27,NULL,NOW(),NOW()),
('G028','Michael Kors Georgie Sneaker','Michael Kors','sneakers','Women',118.00,'USD',4.2,0,28,NULL,NOW(),NOW()),
('G029','SOREL Kinetic Impact II Strap Sneaker','SOREL','sneakers','Women',130.00,'USD',4.4,0,29,NULL,NOW(),NOW()),
('G030','Under Armour Charged Breeze 2','Under Armour','sneakers','Men',90.00,'USD',4.3,0,30,NULL,NOW(),NOW()),
('G031','Red Wing Blacksmith 3352 Moc Toe Boot','Red Wing','boots','Men',319.99,'USD',4.8,0,31,NULL,NOW(),NOW()),
('G032','Wolverine Raider DuraShocks 6" Boot','Wolverine','boots','Men',119.99,'USD',4.4,0,32,NULL,NOW(),NOW()),
('G033','Teva ReEmber Moc','Teva','slippers','Unisex',60.00,'USD',4.4,0,33,NULL,NOW(),NOW()),
('G034','Chaco Z/Cloud X2 Athletic Sandal','Chaco','sandals','Women',125.00,'USD',4.6,0,34,NULL,NOW(),NOW()),
('G035','OluKai Kaona Slip-On','OluKai','sneakers','Men',110.00,'USD',4.5,0,35,NULL,NOW(),NOW()),
('G036','Ecco Biom G5 Golf Shoe','ECCO','sneakers','Men',200.00,'USD',4.5,0,36,NULL,NOW(),NOW()),
('G037','Salomon Sense Ride 5','Salomon','sneakers','Women',120.00,'USD',4.5,0,37,NULL,NOW(),NOW()),
('G038','Reebok Floatride Energy Symmetros 2.5','Reebok','sneakers','Men',100.00,'USD',4.3,0,38,NULL,NOW(),NOW()),
('G039','Puma Fuse 2.0 Training Shoe','Puma','sneakers','Women',75.00,'USD',4.2,0,39,NULL,NOW(),NOW()),
('G040','Vans Warped Hi VR3','Vans','sneakers','Men',80.00,'USD',4.4,0,40,NULL,NOW(),NOW())
ON CONFLICT ("ProductId") WHERE "ProductId" IS NOT NULL DO NOTHING;

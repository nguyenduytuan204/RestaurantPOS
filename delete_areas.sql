DELETE FROM OrderDetails WHERE OrderID IN (SELECT OrderID FROM Orders WHERE TableID IN (SELECT TableID FROM DiningTables WHERE AreaID IN (SELECT AreaID FROM Areas WHERE AreaName LIKE '%Khu A%' OR AreaName LIKE '%Khu B%' OR AreaName LIKE '%VIP%')));
DELETE FROM Orders WHERE TableID IN (SELECT TableID FROM DiningTables WHERE AreaID IN (SELECT AreaID FROM Areas WHERE AreaName LIKE '%Khu A%' OR AreaName LIKE '%Khu B%' OR AreaName LIKE '%VIP%'));
DELETE FROM DiningTables WHERE AreaID IN (SELECT AreaID FROM Areas WHERE AreaName LIKE '%Khu A%' OR AreaName LIKE '%Khu B%' OR AreaName LIKE '%VIP%');
DELETE FROM Areas WHERE AreaName LIKE '%Khu A%' OR AreaName LIKE '%Khu B%' OR AreaName LIKE '%VIP%';

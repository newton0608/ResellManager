BEGIN TRANSACTION;
ALTER TABLE "DetallesCompra" ADD "CostoUnitarioMonedaOrigen" decimal(10,2) NOT NULL DEFAULT '0.0';

ALTER TABLE "Compras" ADD "FechaTipoCambioReferencia" TEXT NULL;

ALTER TABLE "Compras" ADD "FuenteTipoCambio" TEXT NULL;

ALTER TABLE "Compras" ADD "Moneda" TEXT NOT NULL DEFAULT 'GTQ';

ALTER TABLE "Compras" ADD "TipoCambio" TEXT NOT NULL DEFAULT '1.0';

ALTER TABLE "Compras" ADD "TipoCambioReferencia" TEXT NULL;

ALTER TABLE "Compras" ADD "TotalMonedaOrigen" decimal(10,2) NOT NULL DEFAULT '0.0';

UPDATE Compras SET TotalMonedaOrigen = Total;

UPDATE DetallesCompra SET CostoUnitarioMonedaOrigen = CostoUnitario;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261003155826_AddPurchaseCurrencies', '10.0.12');

COMMIT;


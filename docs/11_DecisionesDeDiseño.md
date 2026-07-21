# Decisiones

## 001 La aplicación será una Web App.

Debe funcionar desde iPhone, Android y computadora sin desarrollar aplicaciones nativas.

Alternativas consideradas:
- MAUI
- Flutter
- Aplicación iOS

Resultado:
ASP.NET Core + Blazor

## 002 SQLite como base de datos inicial

Es sencilla, ligera y suficiente para la primera versión.

En el futuro:
Migrar a PostgreSQL si el proyecto crece

## 003 El sistema debe permitir múltiples orígenes de abastecimiento.

- Importación EE.UU.
- Compra local.
- Catálogo (Agora / Andrea)
- Envíos del hijo

## 004 No desarrollar una aplicación móvil nativa.

Motivo:
Una Web App cubre todos los dispositivos.

Resultado:
Blazor.
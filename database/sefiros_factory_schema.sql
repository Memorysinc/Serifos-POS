-- =============================================================================
-- Sefiros POS (.NET 10) — Clean Factory Database Schema & LATAM Seed Data
-- Generated structure-only DDL (zero personal or production data)
-- Default admin user: ADMINISTRA / Password (default): 123456
-- =============================================================================

CREATE DATABASE IF NOT EXISTS `firewallcontrol` CHARACTER SET latin1 COLLATE latin1_swedish_ci;
USE `firewallcontrol`;
SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS `accesoforms`;
CREATE TABLE `accesoforms` (
  `tituloform` varchar(100) NOT NULL DEFAULT '',
  `buscar` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `nuevo` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `editar` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `eliminar` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `imprimir` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `nameform` varchar(80) NOT NULL DEFAULT '',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `id` int(11) NOT NULL AUTO_INCREMENT,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `agenda`;
CREATE TABLE `agenda` (
  `ID` varchar(11) NOT NULL DEFAULT '',
  `FECHA` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `ASUNTO` varchar(100) NOT NULL DEFAULT '',
  `NOTAS` text NOT NULL,
  `AVISO` int(4) NOT NULL DEFAULT '0',
  `TIPOREG` int(1) NOT NULL DEFAULT '0',
  `REGISTRO` varchar(20) NOT NULL DEFAULT '',
  `userid` varchar(25) NOT NULL DEFAULT '',
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ajustes`;
CREATE TABLE `ajustes` (
  `idajuste` int(11) NOT NULL AUTO_INCREMENT,
  `nro_ajuste` int(11) NOT NULL DEFAULT '0',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `autorizadopor` varchar(60) NOT NULL DEFAULT '',
  `motivo` int(1) NOT NULL DEFAULT '0',
  `motivotexto` varchar(60) NOT NULL DEFAULT '',
  `especificar` varchar(200) NOT NULL DEFAULT '',
  `concepto` varchar(60) NOT NULL DEFAULT '',
  `sobrante` decimal(12,2) NOT NULL DEFAULT '0.00',
  `faltante` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_diferencia` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(60) NOT NULL DEFAULT '',
  `nlineno` int(11) NOT NULL DEFAULT '0',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idajuste`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `areas`;
CREATE TABLE `areas` (
  `idarea` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(3) NOT NULL DEFAULT '',
  `descripcion` varchar(80) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idarea`,`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `bancolistas`;
CREATE TABLE `bancolistas` (
  `idbancolista` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(100) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  PRIMARY KEY (`idbancolista`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `bancos`;
CREATE TABLE `bancos` (
  `idbanco` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(100) NOT NULL DEFAULT '',
  `direccion` varchar(200) NOT NULL DEFAULT '',
  `telefonos` varchar(50) NOT NULL DEFAULT '',
  `email` varchar(150) NOT NULL DEFAULT '',
  `cuenta` varchar(50) NOT NULL DEFAULT '',
  `titular` varchar(150) NOT NULL DEFAULT '',
  `contacto` varchar(150) NOT NULL DEFAULT '',
  `web` varchar(150) NOT NULL DEFAULT '',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idbanco`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `barrio`;
CREATE TABLE `barrio` (
  `idbarrio` int(5) NOT NULL AUTO_INCREMENT,
  `barrio` varchar(50) NOT NULL,
  `idciudad` int(5) NOT NULL,
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idbarrio`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `bitacora`;
CREATE TABLE `bitacora` (
  `idbitacora` int(11) NOT NULL AUTO_INCREMENT,
  `idlegajo` int(11) NOT NULL DEFAULT '0',
  `tipo` varchar(60) NOT NULL DEFAULT '',
  `titulo` varchar(80) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `descripcion` varchar(100) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idbitacora`,`idlegajo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cajas`;
CREATE TABLE `cajas` (
  `idcaja` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(60) NOT NULL,
  `saldo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `obs` text NOT NULL,
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcaja`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cargos`;
CREATE TABLE `cargos` (
  `idcargo` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(3) NOT NULL DEFAULT '',
  `descripcion` varchar(80) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcargo`,`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ciudad`;
CREATE TABLE `ciudad` (
  `idciudad` int(5) NOT NULL AUTO_INCREMENT,
  `ciudad` varchar(50) NOT NULL,
  `departamento` varchar(50) NOT NULL,
  PRIMARY KEY (`idciudad`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `clientes`;
CREATE TABLE `clientes` (
  `idcliente` int(5) NOT NULL AUTO_INCREMENT,
  `ruc` varchar(20) DEFAULT NULL,
  `nombres` varchar(120) DEFAULT NULL,
  `contacto` varchar(120) DEFAULT NULL,
  `telefono_contacto` varchar(100) DEFAULT NULL,
  `direccion` text,
  `idciudad` int(5) DEFAULT '0',
  `telefono` varchar(100) DEFAULT NULL,
  `celular` varchar(100) DEFAULT NULL,
  `fax` varchar(12) DEFAULT '',
  `email` varchar(150) DEFAULT NULL,
  `idvendedor` int(5) DEFAULT '0',
  `lineacredito` decimal(20,2) DEFAULT '0.00',
  `fijarprecios` int(1) DEFAULT '0',
  `contado` int(1) DEFAULT '0',
  `credito` int(1) DEFAULT '0',
  `cuota` int(1) DEFAULT '0',
  `estado` int(1) DEFAULT '0',
  `fechaalta` date DEFAULT '0000-00-00',
  `userid` varchar(25) DEFAULT NULL,
  `precio1` int(1) DEFAULT NULL,
  `precio2` int(1) DEFAULT NULL,
  `precio3` int(1) DEFAULT NULL,
  `precio4` int(1) DEFAULT NULL,
  `precio5` int(1) DEFAULT NULL,
  `precio6` int(1) DEFAULT NULL,
  `precio7` int(1) DEFAULT NULL,
  `judicial` varchar(250) DEFAULT '',
  `dv` varchar(2) DEFAULT '',
  `fecha_nacimiento` date DEFAULT NULL,
  `idbarrio` int(5) DEFAULT '0',
  `estadocivil` int(1) DEFAULT '0',
  `nacionalidad` int(1) DEFAULT '0',
  `vivienda` int(1) DEFAULT '0',
  `ips` varchar(50) DEFAULT '',
  `cargo` varchar(50) DEFAULT '',
  `empresa` varchar(80) DEFAULT '',
  `direccionempresa` text,
  `emailempresa` varchar(150) DEFAULT '',
  `idciudadempresa` int(5) DEFAULT '0',
  `idbarrioempresa` int(5) DEFAULT '0',
  `telefonoempresa` varchar(20) DEFAULT '',
  `antiguedad` varchar(20) DEFAULT '',
  `salario` decimal(12,2) DEFAULT '0.00',
  `ref_per1` varchar(80) DEFAULT '',
  `ref_per2` varchar(80) DEFAULT '',
  `ref_per3` varchar(80) DEFAULT '',
  `ref_per_tel1` varchar(20) DEFAULT '',
  `ref_per_tel2` varchar(20) DEFAULT '',
  `ref_per_tel3` varchar(20) DEFAULT '',
  `ref_lab1` varchar(80) DEFAULT '',
  `ref_lab2` varchar(80) DEFAULT '',
  `ref_lab3` varchar(80) DEFAULT '',
  `ref_lab_tel1` varchar(20) DEFAULT '',
  `ref_lab_tel2` varchar(20) DEFAULT '',
  `ref_lab_tel3` varchar(20) DEFAULT '',
  `latitud` varchar(12) DEFAULT '',
  `longitud` varchar(12) DEFAULT '',
  `ruc_conyuge` varchar(20) DEFAULT '',
  `dv_conyuge` int(2) DEFAULT '0',
  `nombres_conyuge` varchar(120) DEFAULT '',
  `fecha_nacimiento_conyuge` date DEFAULT '0000-00-00',
  `nacionalidad_conyuge` int(1) DEFAULT '0',
  `judicial_conyuge` varchar(100) DEFAULT '',
  `telefono_conyuge` varchar(20) DEFAULT '',
  `ips_conyuge` varchar(50) DEFAULT '',
  `cargo_conyuge` varchar(80) DEFAULT '',
  `direccionempresa_conyuge` text,
  `emailempresa_conyuge` varchar(150) DEFAULT '',
  `idciudadempresa_conyuge` int(5) DEFAULT '0',
  `idbarrioempresa_conyuge` int(5) DEFAULT '0',
  `telefonoempresa_conyuge` varchar(20) DEFAULT '',
  `antiguedad_conyuge` varchar(20) DEFAULT '',
  `salario_conyuge` decimal(12,2) DEFAULT '0.00',
  `empresa_conyuge` varchar(120) DEFAULT '',
  `celular_conyuge` varchar(20) DEFAULT '',
  `ref_per11` varchar(50) DEFAULT '',
  `ref_per22` varchar(50) DEFAULT '',
  `ref_per33` varchar(50) DEFAULT '',
  `celularempresa` varchar(20) DEFAULT '',
  `cidusuario` varchar(20) DEFAULT '',
  `idsucursal` int(11) DEFAULT '0',
  `dessucursal` varchar(50) DEFAULT '',
  `propio` int(1) DEFAULT '0',
  `inforconf` int(1) DEFAULT '0',
  `judicial1` int(1) DEFAULT '0',
  `refinanciacion` int(1) DEFAULT '0',
  `fecha_envioinforconf` date DEFAULT '0000-00-00',
  PRIMARY KEY (`idcliente`),
  KEY `idcliente` (`idcliente`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cobradores`;
CREATE TABLE `cobradores` (
  `idcobrador` int(10) NOT NULL AUTO_INCREMENT,
  `nombres` varchar(60) NOT NULL DEFAULT '',
  `documento` varchar(15) NOT NULL DEFAULT '',
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `telefonos` varchar(20) NOT NULL DEFAULT '',
  `fechaalta` date NOT NULL DEFAULT '0000-00-00',
  `celular` varchar(20) NOT NULL DEFAULT '',
  `direccion` varchar(200) NOT NULL DEFAULT '',
  `email` varchar(100) NOT NULL DEFAULT '',
  `observacion` varchar(200) NOT NULL DEFAULT '',
  `comision` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `comisionfija` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idciudad` int(11) NOT NULL DEFAULT '0',
  `codigobarra` varchar(20) NOT NULL DEFAULT '',
  `foto` varchar(100) NOT NULL DEFAULT '',
  `fecha_suspendido` date NOT NULL DEFAULT '0000-00-00',
  `motivosuspendido` varchar(200) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `fechanac` date NOT NULL DEFAULT '0000-00-00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcobrador`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cobrocliente`;
CREATE TABLE `cobrocliente` (
  `idcobro` int(11) NOT NULL AUTO_INCREMENT,
  `idventa` int(11) NOT NULL DEFAULT '0',
  `nro_venta` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cuota` varchar(5) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `dia` int(2) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `cobro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrega` decimal(12,2) NOT NULL DEFAULT '0.00',
  `fecha_cobro` date NOT NULL DEFAULT '0000-00-00',
  `forma_cobro` varchar(20) NOT NULL DEFAULT '',
  `mora` int(5) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  `idcobrador` int(11) NOT NULL DEFAULT '0',
  `obs` varchar(500) NOT NULL DEFAULT '',
  `tipochq` int(2) NOT NULL DEFAULT '0',
  `idbancheq` int(11) NOT NULL DEFAULT '0',
  `numerochq` varchar(20) NOT NULL DEFAULT '',
  `tituchq` varchar(50) NOT NULL DEFAULT '',
  `femichq` date NOT NULL DEFAULT '0000-00-00',
  `fcobchq` date NOT NULL DEFAULT '0000-00-00',
  `tipotar` int(1) NOT NULL DEFAULT '0',
  `idbantar` int(11) NOT NULL DEFAULT '0',
  `numerotar` varchar(20) NOT NULL DEFAULT '',
  `titutar` varchar(50) NOT NULL DEFAULT '',
  `tipotra` int(1) NOT NULL DEFAULT '0',
  `idbantra` int(11) NOT NULL DEFAULT '0',
  `numerotra` varchar(20) NOT NULL DEFAULT '',
  `titutra` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcobro`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `comanda`;
CREATE TABLE `comanda` (
  `idcomanda` int(11) NOT NULL AUTO_INCREMENT,
  `idventa` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `mesa_nro` int(2) NOT NULL DEFAULT '0',
  `barra_nro` int(1) NOT NULL DEFAULT '0',
  `hora` varchar(8) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `plato` varchar(255) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `orden` varchar(15) NOT NULL DEFAULT '',
  `observaciones` varchar(255) NOT NULL DEFAULT '',
  `cantidad` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idcomanda`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `comision_por_proveedor`;
CREATE TABLE `comision_por_proveedor` (
  `idcomision` int(11) NOT NULL AUTO_INCREMENT,
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `comision` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idcomision`) USING BTREE,
  KEY `idvendedor` (`idvendedor`),
  KEY `idproveedor` (`idproveedor`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `compras`;
CREATE TABLE `compras` (
  `idcompra` int(11) NOT NULL AUTO_INCREMENT,
  `nro_compra` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `fecha_compra` date NOT NULL DEFAULT '0000-00-00',
  `forma_pago` int(1) NOT NULL DEFAULT '0',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_compra` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `concepto` varchar(100) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `tipoboleta` int(1) NOT NULL DEFAULT '0',
  `nro_boleta` int(11) NOT NULL DEFAULT '0',
  `observacion` varchar(250) NOT NULL DEFAULT '',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `idmoneda` int(5) NOT NULL DEFAULT '0',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcompra`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `compuesto`;
CREATE TABLE `compuesto` (
  `idcompuesto` int(11) NOT NULL AUTO_INCREMENT,
  `idproductocompuesto` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `idsucursal0` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  `cantidad` decimal(10,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`idcompuesto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `controlcuentaagendas`;
CREATE TABLE `controlcuentaagendas` (
  `idcontrolcuentaagendas` int(11) NOT NULL AUTO_INCREMENT,
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `fecha_agenda` date NOT NULL DEFAULT '0000-00-00',
  `fecha_aviso` date NOT NULL DEFAULT '0000-00-00',
  `nota` text NOT NULL,
  `reclamo` text NOT NULL,
  `estado_agenda` int(1) NOT NULL DEFAULT '0',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcontrolcuentaagendas`),
  KEY `idcliente` (`idcliente`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `controlcuentallamadas`;
CREATE TABLE `controlcuentallamadas` (
  `idcontrolcuentallamada` int(11) NOT NULL AUTO_INCREMENT,
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `fechallamada` date NOT NULL DEFAULT '0000-00-00',
  `monto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcontrolcuentallamada`),
  KEY `idcliente` (`idcliente`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `controlcuentas`;
CREATE TABLE `controlcuentas` (
  `idcontrolcuenta` int(11) NOT NULL AUTO_INCREMENT,
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `totalmora` decimal(12,2) NOT NULL DEFAULT '0.00',
  `mora` decimal(12,2) NOT NULL DEFAULT '0.00',
  `punitorio` decimal(12,2) NOT NULL DEFAULT '0.00',
  `totalmontocuota` decimal(12,2) NOT NULL DEFAULT '0.00',
  `administrativo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `montocobrar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `ordenjudicial` int(1) NOT NULL DEFAULT '0',
  `fecha_insert` date NOT NULL DEFAULT '0000-00-00',
  `fecha_ultima_update` date NOT NULL DEFAULT '0000-00-00',
  `honorariojudicial` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(20) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcontrolcuenta`),
  KEY `idcliente` (`idcliente`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cotizaciones`;
CREATE TABLE `cotizaciones` (
  `idcotizacion` int(11) NOT NULL AUTO_INCREMENT,
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `hora` varchar(8) NOT NULL DEFAULT '',
  `compra_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `compra_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `compra_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `compra_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `compra_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `venta_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `venta_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `venta_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `venta_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `venta_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcotizacion`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `creditocliente`;
CREATE TABLE `creditocliente` (
  `idcredito` int(11) NOT NULL AUTO_INCREMENT,
  `idventa` int(11) NOT NULL DEFAULT '0',
  `nro_venta` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(20) NOT NULL DEFAULT '',
  `total` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrega` decimal(12,2) NOT NULL DEFAULT '0.00',
  `saldo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cuota` varchar(5) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `dia` int(2) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcredito`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cuentapagar`;
CREATE TABLE `cuentapagar` (
  `idcuentapagar` int(11) NOT NULL AUTO_INCREMENT,
  `fecha_carga` date NOT NULL DEFAULT '0000-00-00',
  `fecha_proceso` date NOT NULL DEFAULT '0000-00-00',
  `nro_pagare` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(20) NOT NULL DEFAULT '0',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `total` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrega` decimal(12,2) NOT NULL DEFAULT '0.00',
  `saldo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cuota` int(5) NOT NULL DEFAULT '0',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcuentapagar`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `cuentarecibir`;
CREATE TABLE `cuentarecibir` (
  `idcuentarecibir` int(11) NOT NULL AUTO_INCREMENT,
  `fecha_carga` date NOT NULL DEFAULT '0000-00-00',
  `fecha_proceso` date NOT NULL DEFAULT '0000-00-00',
  `nro_pagare` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `total` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrega` decimal(12,2) NOT NULL DEFAULT '0.00',
  `saldo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cuota` int(5) NOT NULL DEFAULT '0',
  `honorariojudicial` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcuentarecibir`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `datosempresa`;
CREATE TABLE `datosempresa` (
  `iddatosempresa` int(5) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(60) NOT NULL DEFAULT '',
  `ruc` varchar(60) NOT NULL DEFAULT '',
  `direccion` text NOT NULL,
  `telefonos` varchar(60) NOT NULL DEFAULT '',
  `rutafoto` varchar(60) NOT NULL DEFAULT '',
  `nombrecorreo` varchar(60) NOT NULL DEFAULT '',
  `correopredeterminado` varchar(60) NOT NULL DEFAULT '',
  `correosmtp` varchar(60) NOT NULL DEFAULT '',
  `utilidad1` decimal(12,2) NOT NULL DEFAULT '0.00',
  `utilidad2` decimal(12,2) NOT NULL DEFAULT '0.00',
  `utilidad3` decimal(12,2) NOT NULL DEFAULT '0.00',
  `utilidad4` decimal(12,2) NOT NULL DEFAULT '0.00',
  `utilidad5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `utilidad6` decimal(12,2) NOT NULL DEFAULT '0.00',
  `utilidad7` decimal(12,2) NOT NULL DEFAULT '0.00',
  `interescuota` decimal(12,2) NOT NULL DEFAULT '0.00',
  `interespunitorio` decimal(12,2) NOT NULL DEFAULT '0.00',
  `reclamos` decimal(12,2) NOT NULL DEFAULT '0.00',
  `honorariojudicial` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cobro_caja` int(1) NOT NULL DEFAULT '0',
  `ventaitemsdeposito` int(1) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `cantvias` int(1) NOT NULL DEFAULT '0',
  `imprenormal` varchar(30) NOT NULL DEFAULT '',
  `imprebalanza` varchar(30) NOT NULL DEFAULT '',
  `veterinaria` tinyint(1) NOT NULL DEFAULT '0',
  `comercial` tinyint(1) NOT NULL DEFAULT '0',
  `restaurant` tinyint(1) NOT NULL DEFAULT '0',
  `imprecocina` varchar(50) NOT NULL DEFAULT '',
  `imprebarra` varchar(50) NOT NULL DEFAULT '',
  `imprechurra` varchar(50) NOT NULL DEFAULT '',
  `representante` varchar(100) NOT NULL DEFAULT '',
  `ruc_contador` varchar(60) NOT NULL DEFAULT '',
  `idciudad` int(5) NOT NULL DEFAULT '0',
  `idbarrio` int(5) NOT NULL DEFAULT '0',
  `imprelegal` varchar(50) NOT NULL DEFAULT '',
  `habilita_cobro` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`iddatosempresa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `departamentos`;
CREATE TABLE `departamentos` (
  `iddepartamento` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(3) NOT NULL DEFAULT '',
  `descripcion` varchar(80) NOT NULL DEFAULT '',
  PRIMARY KEY (`iddepartamento`,`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `depositos`;
CREATE TABLE `depositos` (
  `iddeposito` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(80) NOT NULL DEFAULT '',
  `responsable` varchar(80) NOT NULL DEFAULT '',
  `estado` int(11) NOT NULL DEFAULT '0',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`iddeposito`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `descuentopersonal`;
CREATE TABLE `descuentopersonal` (
  `iddescuentopersonal` int(11) NOT NULL AUTO_INCREMENT,
  `idlegajo` int(11) NOT NULL DEFAULT '0',
  `documento` varchar(20) NOT NULL DEFAULT '',
  `nombres` varchar(150) NOT NULL DEFAULT '',
  `fecha_descuento` date NOT NULL DEFAULT '0000-00-00',
  `monto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cuota` int(5) NOT NULL DEFAULT '0',
  `saldo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `iddescuento` int(11) NOT NULL DEFAULT '0',
  `coddescuento` varchar(5) NOT NULL DEFAULT '',
  `desdescuento` varchar(80) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `monto_cuota` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iddescuentopersonal`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `descuentos`;
CREATE TABLE `descuentos` (
  `iddescuento` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(3) NOT NULL DEFAULT '',
  `descripcion` varchar(30) NOT NULL DEFAULT '',
  PRIMARY KEY (`iddescuento`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `deudaproveedor`;
CREATE TABLE `deudaproveedor` (
  `iddeuda` int(11) NOT NULL AUTO_INCREMENT,
  `idcompra` int(11) NOT NULL DEFAULT '0',
  `nro_compra` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(20) NOT NULL DEFAULT '',
  `total` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrega` decimal(12,2) NOT NULL DEFAULT '0.00',
  `saldo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cuota` varchar(5) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `dia` int(2) NOT NULL DEFAULT '0',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`iddeuda`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `devolucioncompras`;
CREATE TABLE `devolucioncompras` (
  `iddevolucioncompra` int(11) NOT NULL AUTO_INCREMENT,
  `idcompra` int(11) NOT NULL DEFAULT '0',
  `nro_boleta` varchar(20) NOT NULL DEFAULT '',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `desproveedor` varchar(150) NOT NULL DEFAULT '',
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `direccion` varchar(250) NOT NULL DEFAULT '',
  `fecha_devolucion` date NOT NULL DEFAULT '0000-00-00',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_compra` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `forma_pago` varchar(20) NOT NULL DEFAULT '',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `concepto` text,
  PRIMARY KEY (`iddevolucioncompra`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `devolucionventas`;
CREATE TABLE `devolucionventas` (
  `iddevolucionventa` int(11) NOT NULL AUTO_INCREMENT,
  `idventa` int(11) NOT NULL DEFAULT '0',
  `nro_boleta` varchar(20) NOT NULL DEFAULT '',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `descliente` varchar(150) NOT NULL DEFAULT '',
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `direccion` varchar(250) NOT NULL DEFAULT '',
  `fecha_devolucion` date NOT NULL DEFAULT '0000-00-00',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `forma_pago2` varchar(20) NOT NULL DEFAULT '',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `concepto` text,
  PRIMARY KEY (`iddevolucionventa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `factura_legal`;
CREATE TABLE `factura_legal` (
  `idfacturalegal` int(11) NOT NULL AUTO_INCREMENT,
  `n1` int(3) NOT NULL DEFAULT '0',
  `n2` int(3) NOT NULL DEFAULT '0',
  `n3` int(7) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `idventa` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idfacturalegal`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `formapago`;
CREATE TABLE `formapago` (
  `idformapago` int(11) DEFAULT NULL,
  `descripcion` varchar(50) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `fw_config_global`;
CREATE TABLE `fw_config_global` (
  `clave` varchar(80) NOT NULL,
  `valor` varchar(250) NOT NULL,
  `actualizado` datetime NOT NULL,
  PRIMARY KEY (`clave`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

DROP TABLE IF EXISTS `garantiaproductos`;
CREATE TABLE `garantiaproductos` (
  `idgarantiaproducto` int(11) NOT NULL AUTO_INCREMENT,
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `idventa` int(11) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(25) NOT NULL DEFAULT '',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  `fecha_venta` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`idgarantiaproducto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=COMPACT;

DROP TABLE IF EXISTS `grupoproductos`;
CREATE TABLE `grupoproductos` (
  `idgrupoproducto` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(150) NOT NULL DEFAULT '',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `fechainicio` date NOT NULL DEFAULT '0000-00-00',
  `fechafinal` date NOT NULL DEFAULT '0000-00-00',
  `impresora` varchar(180) NOT NULL DEFAULT '',
  PRIMARY KEY (`idgrupoproducto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `grupos`;
CREATE TABLE `grupos` (
  `Nlineno` int(11) NOT NULL AUTO_INCREMENT,
  `Cidgrupo` varchar(10) NOT NULL DEFAULT '',
  `Cdescribe` varchar(45) DEFAULT '',
  `Caccesos` varchar(255) NOT NULL DEFAULT '',
  `avisos` int(1) NOT NULL DEFAULT '0',
  `tool` int(1) NOT NULL DEFAULT '0',
  `cidusuario` varchar(10) NOT NULL DEFAULT '',
  `btncompra` int(1) NOT NULL DEFAULT '0',
  `btnventa` int(1) NOT NULL DEFAULT '0',
  `btnpedido` int(1) NOT NULL DEFAULT '0',
  `btnpresu` int(1) NOT NULL DEFAULT '0',
  `btnvenci` int(1) NOT NULL DEFAULT '0',
  `btncliente` int(1) NOT NULL DEFAULT '0',
  `btnproveedor` int(1) NOT NULL DEFAULT '0',
  `btnvendedor` int(1) NOT NULL DEFAULT '0',
  `btnproducto` int(1) NOT NULL DEFAULT '0',
  `btnpago` int(1) NOT NULL DEFAULT '0',
  `btncobro` int(1) NOT NULL DEFAULT '0',
  `btnmovcaja` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Nlineno`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `imp_solicitudcredito`;
CREATE TABLE `imp_solicitudcredito` (
  `idsolicitudcredito` int(11) NOT NULL AUTO_INCREMENT,
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `desvendedor` varchar(80) NOT NULL DEFAULT '',
  `cantidadsolicitada` int(12) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idsolicitudcredito`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemsajustes`;
CREATE TABLE `itemsajustes` (
  `iditemajuste` int(11) NOT NULL AUTO_INCREMENT,
  `idajuste` int(11) NOT NULL DEFAULT '0',
  `nro_ajuste` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `salida` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrada` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iditemajuste`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemscompras`;
CREATE TABLE `itemscompras` (
  `iditemcompra` int(11) NOT NULL AUTO_INCREMENT,
  `idcompra` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_compra` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iditemcompra`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemsdepositos`;
CREATE TABLE `itemsdepositos` (
  `iditemdeposito` int(11) NOT NULL AUTO_INCREMENT,
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iditemdeposito`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemsdevolucioncompras`;
CREATE TABLE `itemsdevolucioncompras` (
  `iditemdevolucioncompra` int(11) NOT NULL AUTO_INCREMENT,
  `iddevolucioncompra` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_compra` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`iditemdevolucioncompra`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemsdevolucionventas`;
CREATE TABLE `itemsdevolucionventas` (
  `iditemdevolucionventa` int(11) NOT NULL AUTO_INCREMENT,
  `iddevolucionventa` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_venta` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`iditemdevolucionventa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemspedidos`;
CREATE TABLE `itemspedidos` (
  `iditempedido` int(11) NOT NULL AUTO_INCREMENT,
  `idpedido` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_pedido` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iditempedido`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemspresupuestos`;
CREATE TABLE `itemspresupuestos` (
  `iditempresupuesto` int(11) NOT NULL AUTO_INCREMENT,
  `idpresupuesto` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_presupuesto` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`iditempresupuesto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemspreventas`;
CREATE TABLE `itemspreventas` (
  `iditempreventa` int(11) NOT NULL AUTO_INCREMENT,
  `idpreventa` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_preventa` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`iditempreventa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemssolicitudcredito`;
CREATE TABLE `itemssolicitudcredito` (
  `iditemsolicitud` int(11) NOT NULL AUTO_INCREMENT,
  `idsolicitud` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codproducto` varchar(20) NOT NULL DEFAULT '',
  `desproducto` varchar(150) NOT NULL DEFAULT '',
  `precio` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iditemsolicitud`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `itemsventas`;
CREATE TABLE `itemsventas` (
  `iditemventa` int(11) NOT NULL AUTO_INCREMENT,
  `idventa` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `nro_venta` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_unitario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `pre_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `descripcion` varchar(60) NOT NULL DEFAULT '',
  `precio_total` decimal(12,2) NOT NULL DEFAULT '0.00',
  `estado` int(1) NOT NULL DEFAULT '0',
  `productoguarnicion` int(1) NOT NULL DEFAULT '0',
  `impresora` varchar(80) NOT NULL DEFAULT '',
  `imprimido` int(1) NOT NULL DEFAULT '0',
  `observaciones` varchar(80) NOT NULL DEFAULT '',
  `comanda` int(1) NOT NULL DEFAULT '0',
  `barra` int(1) NOT NULL DEFAULT '0',
  `cocina` int(1) NOT NULL DEFAULT '0',
  `churrasqueria` int(1) NOT NULL DEFAULT '0',
  `obs` varchar(100) NOT NULL DEFAULT '',
  `facturado` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`iditemventa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `judiciales`;
CREATE TABLE `judiciales` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `CIDCAP` int(11) NOT NULL,
  `ORDEN` int(11) NOT NULL,
  `CLAVE` int(11) NOT NULL,
  `CAUSA` varchar(50) NOT NULL,
  `UNIDAD` int(11) NOT NULL,
  `FECNOTA` int(11) NOT NULL,
  `NRONOTA` int(11) NOT NULL,
  `COMPETE` int(11) NOT NULL,
  `TURNO` int(11) NOT NULL,
  `CIRCUN` int(11) NOT NULL,
  `LUGARH` varchar(15) NOT NULL,
  `JUEZ` varchar(30) NOT NULL,
  `SECRETA` varchar(30) NOT NULL,
  `INTERINO` int(11) NOT NULL,
  `FECHAGRA` int(11) NOT NULL,
  `USUARIO` varchar(4) NOT NULL,
  `HORAGRA` int(11) NOT NULL,
  `ESTADO` varchar(4) NOT NULL,
  `OBSERVA` varchar(30) NOT NULL,
  `RADIO` varchar(10) NOT NULL,
  `FECRADIO` int(11) NOT NULL,
  `UNIDAINF` int(11) NOT NULL,
  `NOBRE` varchar(20) NOT NULL,
  `GUARDI` varchar(4) NOT NULL,
  `HORAGUA` int(11) NOT NULL,
  `CREDEN` int(11) NOT NULL,
  `SITU` varchar(1) NOT NULL,
  `TIPOSE` varchar(2) NOT NULL,
  `NROSE` int(11) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `legajos`;
CREATE TABLE `legajos` (
  `idlegajo` int(11) NOT NULL AUTO_INCREMENT,
  `apellidos` varchar(80) NOT NULL DEFAULT '',
  `nombres` varchar(80) NOT NULL DEFAULT '',
  `nacionalidad` varchar(2) NOT NULL DEFAULT '',
  `desnacionalidad` varchar(80) NOT NULL DEFAULT '',
  `tipo_doc` varchar(2) NOT NULL DEFAULT '',
  `cargo` varchar(3) NOT NULL DEFAULT '',
  `descargo` varchar(80) NOT NULL DEFAULT '',
  `documento` varchar(20) NOT NULL DEFAULT '',
  `cuenta` varchar(20) NOT NULL DEFAULT '',
  `telefono` varchar(80) NOT NULL DEFAULT '',
  `direccion` varchar(250) NOT NULL DEFAULT '',
  `fecha_nacimiento` date NOT NULL DEFAULT '0000-00-00',
  `fecha_ingreso` date NOT NULL DEFAULT '0000-00-00',
  `salario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `foto` varchar(100) NOT NULL DEFAULT '',
  `correo` varchar(150) NOT NULL DEFAULT '',
  `estadocivil` int(1) NOT NULL DEFAULT '0',
  `ips_nro` int(15) NOT NULL DEFAULT '0',
  `sexo` int(1) NOT NULL DEFAULT '0',
  `lugar_nac` varchar(80) NOT NULL DEFAULT '',
  `departamento` varchar(3) NOT NULL DEFAULT '',
  `desdepartamento` varchar(80) NOT NULL DEFAULT '',
  `area` varchar(3) NOT NULL DEFAULT '',
  `desarea` varchar(80) NOT NULL DEFAULT '',
  PRIMARY KEY (`idlegajo`,`nacionalidad`,`tipo_doc`,`cargo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `listasbancos`;
CREATE TABLE `listasbancos` (
  `idlistabanco` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(150) NOT NULL DEFAULT '',
  PRIMARY KEY (`idlistabanco`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `lote_producto`;
CREATE TABLE `lote_producto` (
  `idlote` int(11) NOT NULL AUTO_INCREMENT,
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `lote` varchar(15) NOT NULL DEFAULT '',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idlote`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `marcaproductos`;
CREATE TABLE `marcaproductos` (
  `idmarcaproducto` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(150) NOT NULL DEFAULT '',
  PRIMARY KEY (`idmarcaproducto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `mensajesred`;
CREATE TABLE `mensajesred` (
  `_id` varchar(12) NOT NULL DEFAULT '',
  `usuario1` varchar(70) NOT NULL DEFAULT '',
  `usuario2` varchar(70) NOT NULL DEFAULT '',
  `titulo` varchar(70) NOT NULL DEFAULT '',
  `fecha` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `mensaje` text,
  `ip` varchar(25) NOT NULL DEFAULT '',
  `activo` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `seguro` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `estado` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `adjunto` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `leido` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `nivel` tinyint(3) unsigned NOT NULL DEFAULT '0',
  `username` varchar(30) NOT NULL DEFAULT '',
  `archivo` varchar(255) NOT NULL DEFAULT '',
  PRIMARY KEY (`_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `mesas`;
CREATE TABLE `mesas` (
  `idmesa` int(11) NOT NULL AUTO_INCREMENT,
  `nro_mesa` int(5) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idmesa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `metasventas`;
CREATE TABLE `metasventas` (
  `idmeta` int(11) NOT NULL AUTO_INCREMENT,
  `desde` decimal(12,2) NOT NULL DEFAULT '0.00',
  `hasta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`idmeta`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `modeloproductos`;
CREATE TABLE `modeloproductos` (
  `idmodeloproducto` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(150) NOT NULL DEFAULT '',
  PRIMARY KEY (`idmodeloproducto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `monedas`;
CREATE TABLE `monedas` (
  `idmoneda` int(3) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(30) NOT NULL,
  `compra` decimal(12,2) NOT NULL DEFAULT '0.00',
  `venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `decimales` int(6) NOT NULL DEFAULT '2',
  `tronco` int(1) NOT NULL DEFAULT '0',
  `operacion` varchar(1) NOT NULL DEFAULT '*',
  `tipo` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idmoneda`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='InnoDB free: 2336768 kB';

DROP TABLE IF EXISTS `movimientocaja`;
CREATE TABLE `movimientocaja` (
  `idmovimientocaja` int(10) NOT NULL AUTO_INCREMENT,
  `nrorubro` int(10) NOT NULL DEFAULT '0',
  `ingresos` decimal(12,2) NOT NULL DEFAULT '0.00',
  `egresos` decimal(12,2) NOT NULL DEFAULT '0.00',
  `tipo` varchar(30) NOT NULL,
  `detalle` varchar(200) NOT NULL DEFAULT '',
  `fechaalta` date NOT NULL DEFAULT '0000-00-00',
  `hora` varchar(8) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `idventas` int(15) NOT NULL DEFAULT '0',
  `idcompras` int(15) NOT NULL DEFAULT '0',
  `idcaja` int(5) NOT NULL DEFAULT '0',
  `idmoneda` int(11) NOT NULL DEFAULT '0',
  `cidusuario` varchar(30) NOT NULL DEFAULT '',
  `obs` varchar(200) NOT NULL DEFAULT '',
  PRIMARY KEY (`idmovimientocaja`),
  KEY `nrorubro` (`nrorubro`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `nacionalidades`;
CREATE TABLE `nacionalidades` (
  `idnacionalidad` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(2) NOT NULL DEFAULT '',
  `descripcion` varchar(30) NOT NULL DEFAULT '',
  PRIMARY KEY (`idnacionalidad`,`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `organizador`;
CREATE TABLE `organizador` (
  `idagenda` int(11) NOT NULL AUTO_INCREMENT,
  `nombres` varchar(80) NOT NULL DEFAULT '',
  `telefonos1` varchar(50) NOT NULL DEFAULT '',
  `telefonos2` varchar(50) NOT NULL DEFAULT '',
  `telefonos3` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idagenda`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `pagares`;
CREATE TABLE `pagares` (
  `idpagare` int(11) NOT NULL AUTO_INCREMENT,
  `nro_pagare` int(11) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `emision` date NOT NULL DEFAULT '0000-00-00',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `nro_factura` varchar(20) NOT NULL DEFAULT '',
  `total_venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `dia` int(2) NOT NULL DEFAULT '0',
  `mes` varchar(20) NOT NULL DEFAULT '',
  `año` int(5) NOT NULL DEFAULT '0',
  `vencimientoletra` varchar(60) NOT NULL DEFAULT '',
  `nombreempresa` varchar(60) NOT NULL DEFAULT '',
  `cantidadletra` varchar(150) NOT NULL DEFAULT '',
  `domicilio` varchar(200) NOT NULL DEFAULT '',
  `nombredeudor` varchar(80) NOT NULL DEFAULT '',
  `rucdeudor` varchar(20) NOT NULL DEFAULT '',
  `direcciondeudor` varchar(200) NOT NULL DEFAULT '',
  `nombrecodeudor` varchar(80) NOT NULL DEFAULT '',
  `ruccodeudor` varchar(20) NOT NULL DEFAULT '',
  `direccioncodeudor` varchar(200) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idpagare`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `pagoproveedor`;
CREATE TABLE `pagoproveedor` (
  `idpago` int(11) NOT NULL AUTO_INCREMENT,
  `idcompra` int(11) NOT NULL DEFAULT '0',
  `nro_compra` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `cuota` varchar(5) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `dia` int(2) NOT NULL DEFAULT '0',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `pago` decimal(12,2) NOT NULL DEFAULT '0.00',
  `entrega` decimal(12,2) NOT NULL DEFAULT '0.00',
  `fecha_pago` date NOT NULL DEFAULT '0000-00-00',
  `obs` varchar(500) NOT NULL DEFAULT '',
  PRIMARY KEY (`idpago`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `pagos`;
CREATE TABLE `pagos` (
  `idpago` int(11) NOT NULL AUTO_INCREMENT,
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `idsupervisor` int(11) NOT NULL DEFAULT '0',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `monto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `motivo` int(1) NOT NULL DEFAULT '0',
  `meses` int(2) NOT NULL DEFAULT '0',
  `año` int(5) NOT NULL DEFAULT '0',
  `opcion` int(1) NOT NULL DEFAULT '0',
  `observaciones` varchar(200) NOT NULL DEFAULT '',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `nombres` varchar(150) NOT NULL DEFAULT '',
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `celular` varchar(20) NOT NULL DEFAULT '',
  `telefono` varchar(20) NOT NULL DEFAULT '',
  `direccion` varchar(200) NOT NULL DEFAULT '',
  `comision` decimal(12,2) NOT NULL DEFAULT '0.00',
  `comisionfija` decimal(12,2) NOT NULL DEFAULT '0.00',
  `totalventa` decimal(12,2) NOT NULL DEFAULT '0.00',
  `comisionventa` decimal(12,2) NOT NULL DEFAULT '0.00',
  `totalcomision` decimal(12,2) NOT NULL DEFAULT '0.00',
  `totaladelanto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `totalrecibir` decimal(12,2) NOT NULL DEFAULT '0.00',
  `metabase` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `dessucursal` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`idpago`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `pedidos`;
CREATE TABLE `pedidos` (
  `idpedido` int(11) NOT NULL AUTO_INCREMENT,
  `nro_pedido` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `fecha_pedido` date NOT NULL DEFAULT '0000-00-00',
  `forma_pago` int(1) NOT NULL DEFAULT '0',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_pedido` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `concepto` varchar(100) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `observacion` varchar(250) NOT NULL DEFAULT '',
  `facturado` int(1) NOT NULL DEFAULT '0',
  `idmoneda` int(5) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idpedido`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `personas`;
CREATE TABLE `personas` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `Nomape` varchar(50) NOT NULL DEFAULT '',
  `Direccion` varchar(40) NOT NULL DEFAULT '',
  `Cedulan` varchar(10) NOT NULL DEFAULT '0',
  `ruc` varchar(13) NOT NULL DEFAULT '',
  `tel` varchar(30) NOT NULL DEFAULT '',
  `email` varchar(30) NOT NULL DEFAULT '',
  `Fec_naci` varchar(10) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  KEY `cedulan` (`Cedulan`),
  KEY `nomape` (`Nomape`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `precio nuevo`;
CREATE TABLE `precio nuevo` (
  `A` varchar(255) DEFAULT NULL,
  `B` varchar(255) DEFAULT NULL,
  `C` varchar(255) DEFAULT NULL,
  `D` varchar(255) DEFAULT NULL,
  `E` varchar(255) DEFAULT NULL,
  `F` varchar(255) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `presupuestos`;
CREATE TABLE `presupuestos` (
  `idpresupuesto` int(11) NOT NULL AUTO_INCREMENT,
  `nro_presupuesto` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `fecha_presupuesto` date NOT NULL DEFAULT '0000-00-00',
  `forma_pago` int(1) NOT NULL DEFAULT '0',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_presupuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `concepto` varchar(100) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `tipoboleta` int(1) NOT NULL DEFAULT '0',
  `nro_boleta` int(11) NOT NULL DEFAULT '0',
  `cot_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `observacion` varchar(12) NOT NULL DEFAULT '',
  `idmoneda` int(5) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idpresupuesto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `preventas`;
CREATE TABLE `preventas` (
  `idpreventa` int(11) NOT NULL AUTO_INCREMENT,
  `nro_preventa` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `fecha_preventa` date NOT NULL DEFAULT '0000-00-00',
  `forma_pago` int(1) NOT NULL DEFAULT '0',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_preventa` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `concepto` varchar(100) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `tipoboleta` int(1) NOT NULL DEFAULT '0',
  `nro_boleta` int(11) NOT NULL DEFAULT '0',
  `cot_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `observacion` varchar(250) NOT NULL DEFAULT '',
  `facturado` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idpreventa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `productos`;
CREATE TABLE `productos` (
  `idproducto` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(25) NOT NULL DEFAULT '',
  `descripcion` varchar(250) NOT NULL DEFAULT '',
  `idmoneda` int(11) NOT NULL DEFAULT '0',
  `cambio` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costomedio` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costo1` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p1` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio1` decimal(12,2) NOT NULL DEFAULT '0.00',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `referencia` varchar(100) NOT NULL DEFAULT '',
  `idgrupoproducto` int(11) NOT NULL DEFAULT '0',
  `idmarcaproducto` int(11) NOT NULL DEFAULT '0',
  `familia` varchar(100) NOT NULL DEFAULT '',
  `aplicacion` text NOT NULL,
  `stockminimo` int(12) NOT NULL DEFAULT '0',
  `stockmaximo` int(12) NOT NULL DEFAULT '0',
  `idsubgrupoproducto` int(11) NOT NULL DEFAULT '0',
  `idmodeloproducto` int(11) NOT NULL DEFAULT '0',
  `observacion` text NOT NULL,
  `costo2` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p2` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio2` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costo3` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p3` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio3` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costo4` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p4` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio4` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costo5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costo6` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p6` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio6` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costo7` decimal(12,2) NOT NULL DEFAULT '0.00',
  `p7` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio7` decimal(12,2) NOT NULL DEFAULT '0.00',
  `estado` int(1) NOT NULL DEFAULT '0',
  `iva` int(1) NOT NULL DEFAULT '0',
  `nocontrolastock` int(1) NOT NULL DEFAULT '0',
  `usaserial` int(1) NOT NULL DEFAULT '0',
  `imprimefactura` int(1) NOT NULL DEFAULT '0',
  `autocantidad` int(1) NOT NULL DEFAULT '0',
  `autoprecio` int(1) NOT NULL DEFAULT '0',
  `usavencimiento` int(1) NOT NULL DEFAULT '0',
  `descripcioneditable` int(1) NOT NULL DEFAULT '0',
  `vendesinstock` int(1) NOT NULL DEFAULT '0',
  `autosuma` int(1) NOT NULL DEFAULT '0',
  `publicarweb` int(1) NOT NULL DEFAULT '0',
  `fraccionable` int(1) NOT NULL DEFAULT '0',
  `compuesto` int(1) NOT NULL DEFAULT '0',
  `codigobarra` varchar(30) NOT NULL DEFAULT '',
  `impuesto` int(2) NOT NULL DEFAULT '0',
  `imagen` varchar(60) NOT NULL DEFAULT '',
  `fechaultimacompra` date NOT NULL DEFAULT '0000-00-00',
  `cantidadultimacompra` decimal(12,2) NOT NULL DEFAULT '0.00',
  `fechaultimaventa` date NOT NULL DEFAULT '0000-00-00',
  `cantidadultimaventa` decimal(12,2) NOT NULL DEFAULT '0.00',
  `costomonedaextranjera` decimal(12,2) NOT NULL DEFAULT '0.00',
  `controlagarantia` int(1) NOT NULL DEFAULT '0',
  `fechainiciogarantia` date NOT NULL DEFAULT '0000-00-00',
  `diasgarantias` int(5) NOT NULL DEFAULT '0',
  `fechafingarantia` date NOT NULL DEFAULT '0000-00-00',
  `muestrabusqueda` int(1) NOT NULL DEFAULT '0',
  `guarnicion` int(1) NOT NULL DEFAULT '0',
  `menu` int(1) NOT NULL DEFAULT '0',
  `comanda` bit(1) NOT NULL DEFAULT b'0',
  `cocina` int(1) NOT NULL DEFAULT '0',
  `otros` int(11) NOT NULL DEFAULT '0',
  `churrasqueria` int(1) NOT NULL DEFAULT '0',
  `barra` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idproducto`),
  KEY `descripcion` (`descripcion`),
  KEY `codigo` (`codigo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `proveedores`;
CREATE TABLE `proveedores` (
  `idproveedor` int(11) NOT NULL AUTO_INCREMENT,
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `descripcion` varchar(150) NOT NULL DEFAULT '',
  `contacto` varchar(150) NOT NULL DEFAULT '',
  `direccion` varchar(250) NOT NULL DEFAULT '',
  `telefono` varchar(100) NOT NULL DEFAULT '',
  `celular` varchar(100) NOT NULL DEFAULT '',
  `fax` varchar(100) NOT NULL DEFAULT '',
  `email` varchar(150) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `userid` varchar(20) NOT NULL DEFAULT '',
  `idciudad` int(11) NOT NULL DEFAULT '0',
  `agenda` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idproveedor`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `recibos`;
CREATE TABLE `recibos` (
  `idrecibos` int(11) NOT NULL AUTO_INCREMENT,
  `letra` varchar(1) NOT NULL DEFAULT '',
  `recibo` varchar(10) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `idcliente` int(10) NOT NULL DEFAULT '0',
  `importe` decimal(12,2) NOT NULL DEFAULT '0.00',
  `concepto` text NOT NULL,
  `montoenletras` varchar(200) NOT NULL DEFAULT '',
  `idcaja` int(10) NOT NULL DEFAULT '0',
  `estado` int(1) NOT NULL DEFAULT '0',
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `idsupervisor` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idrecibos`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `resolucion90_archivos`;
CREATE TABLE `resolucion90_archivos` (
  `nombre` varchar(100) NOT NULL DEFAULT '',
  `fecha` date NOT NULL,
  `tipo` varchar(1) NOT NULL DEFAULT '',
  `version` varchar(5) NOT NULL DEFAULT '',
  `ruc` varchar(30) NOT NULL DEFAULT '',
  `periodo` varchar(4) NOT NULL DEFAULT ''
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `rubros`;
CREATE TABLE `rubros` (
  `idrubros` int(10) NOT NULL AUTO_INCREMENT,
  `nrorubro` int(10) NOT NULL DEFAULT '0',
  `nombrerubro` varchar(35) NOT NULL DEFAULT '',
  `tipocuenta` int(1) NOT NULL DEFAULT '0',
  `proceso` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idrubros`),
  KEY `rubros` (`idrubros`,`nrorubro`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ruc`;
CREATE TABLE `ruc` (
  `id_ruc` int(11) NOT NULL AUTO_INCREMENT,
  `NRORUC` varchar(15) NOT NULL DEFAULT '',
  `RUCANT` varchar(11) NOT NULL DEFAULT '',
  `RUC` varchar(11) NOT NULL DEFAULT '',
  `DV` int(11) NOT NULL DEFAULT '0',
  `DENOMINACI` varchar(80) NOT NULL DEFAULT '',
  `NOM_COMERC` varchar(60) NOT NULL DEFAULT '',
  `TIPO` varchar(21) NOT NULL DEFAULT '',
  `CATEGORIA` varchar(21) NOT NULL DEFAULT '',
  `TIPO_SOC` varchar(30) NOT NULL DEFAULT '',
  `MAIL` varchar(60) NOT NULL DEFAULT '',
  `PAIS` varchar(9) NOT NULL DEFAULT '',
  `DPTO` varchar(20) NOT NULL DEFAULT '',
  `LOCALIDAD` varchar(30) NOT NULL DEFAULT '',
  `DIRECCION` varchar(80) NOT NULL DEFAULT '',
  `TELEFONO` varchar(20) NOT NULL DEFAULT '',
  `INIACTI` date NOT NULL DEFAULT '0000-00-00',
  `ACTIVIDAD` varchar(187) NOT NULL DEFAULT '',
  `ACTIVIDAD1` varchar(187) NOT NULL DEFAULT '',
  `ACTIVIDAD2` varchar(187) NOT NULL DEFAULT '',
  `ESTADO` varchar(12) NOT NULL DEFAULT '',
  `CATASTRO` varchar(30) NOT NULL DEFAULT '',
  PRIMARY KEY (`id_ruc`,`NRORUC`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ruc3`;
CREATE TABLE `ruc3` (
  `ruc` varchar(15) DEFAULT '',
  `descripcion` varchar(255) DEFAULT NULL,
  `dv` int(4) DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ruc6`;
CREATE TABLE `ruc6` (
  `ruc` varchar(15) DEFAULT '',
  `descripcion` varchar(255) DEFAULT NULL,
  `dv` int(4) DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ruc9`;
CREATE TABLE `ruc9` (
  `1000007` double DEFAULT NULL,
  `ROMERO GOMEZ, ALFREDO` varchar(255) DEFAULT NULL,
  `0` double DEFAULT NULL,
  `ROGA652940D` varchar(255) DEFAULT NULL,
  `CANCELADO` varchar(255) DEFAULT NULL,
  `F6` varchar(255) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `salarios`;
CREATE TABLE `salarios` (
  `idsalario` int(11) NOT NULL AUTO_INCREMENT,
  `mes` varchar(20) NOT NULL DEFAULT '',
  `año` int(4) NOT NULL DEFAULT '0',
  `idlegajo` int(11) NOT NULL DEFAULT '0',
  `documento` varchar(20) NOT NULL DEFAULT '',
  `desnombres` varchar(150) NOT NULL DEFAULT '',
  `dias_trabajados` int(5) NOT NULL DEFAULT '0',
  `horas_extra` int(5) NOT NULL DEFAULT '0',
  `monto_bonificacion` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_ingreso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuentos` decimal(12,2) NOT NULL DEFAULT '0.00',
  `salario` decimal(12,2) NOT NULL DEFAULT '0.00',
  `monto_horas_extras` decimal(12,2) NOT NULL DEFAULT '0.00',
  `bonificacion_familiar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `monto_ips` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_egresos` decimal(12,2) NOT NULL DEFAULT '0.00',
  `neto_percibir` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cantidad_feriado_mes` int(5) NOT NULL DEFAULT '0',
  `fecha_pago_salario` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`idsalario`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `salariosparametros`;
CREATE TABLE `salariosparametros` (
  `idsalarioparametro` int(11) NOT NULL AUTO_INCREMENT,
  `salario_minimo` decimal(12,2) NOT NULL DEFAULT '0.00',
  `ips_patronal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `ips_empleado` decimal(12,2) NOT NULL DEFAULT '0.00',
  `bonificacion_familiar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `limite_edad_hijos` int(2) NOT NULL DEFAULT '0',
  `precio_hora_extra` decimal(12,2) NOT NULL DEFAULT '0.00',
  `nro_ips_patronal` int(12) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idsalarioparametro`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `sms_conexion`;
CREATE TABLE `sms_conexion` (
  `idconexion` int(11) NOT NULL AUTO_INCREMENT,
  `descrip` varchar(25) NOT NULL DEFAULT '',
  `puerto` int(11) NOT NULL DEFAULT '0',
  `bautrate` varchar(7) NOT NULL DEFAULT '',
  `databits` varchar(4) NOT NULL DEFAULT '',
  `stopbits` varchar(4) NOT NULL DEFAULT '',
  `parity` varchar(5) NOT NULL DEFAULT '',
  `flow` varchar(8) NOT NULL DEFAULT '',
  `inactivo` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idconexion`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `solicitudcredito`;
CREATE TABLE `solicitudcredito` (
  `idsolicitud` int(11) NOT NULL AUTO_INCREMENT,
  `idcliente` int(11) NOT NULL,
  `monto` decimal(12,2) NOT NULL,
  `plazo` int(5) NOT NULL,
  `cuota` decimal(12,2) NOT NULL,
  `fechaingreso` date NOT NULL,
  `estado` int(1) NOT NULL,
  `observaciones` varchar(250) NOT NULL,
  `fecha_aprobacion` date NOT NULL,
  `idbancolista` int(5) NOT NULL,
  `idvendedor` int(11) NOT NULL,
  `obs_estado` varchar(250) NOT NULL DEFAULT '',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codproducto` varchar(20) NOT NULL DEFAULT '',
  `desbanco` varchar(80) NOT NULL DEFAULT '',
  PRIMARY KEY (`idsolicitud`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `subgrupoproductos`;
CREATE TABLE `subgrupoproductos` (
  `idsubgrupoproducto` int(11) NOT NULL AUTO_INCREMENT,
  `descripcion` varchar(150) NOT NULL DEFAULT '',
  PRIMARY KEY (`idsubgrupoproducto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `sucursal`;
CREATE TABLE `sucursal` (
  `idsucursal` int(11) NOT NULL AUTO_INCREMENT,
  `nombre` varchar(80) NOT NULL DEFAULT '',
  `dir` varchar(150) NOT NULL DEFAULT '',
  `tel` varchar(50) NOT NULL DEFAULT '',
  `cel` varchar(50) NOT NULL DEFAULT '',
  `enc` varchar(80) NOT NULL DEFAULT '',
  `email` varchar(100) NOT NULL DEFAULT '',
  PRIMARY KEY (`idsucursal`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `supervisores`;
CREATE TABLE `supervisores` (
  `idsupervisor` int(10) NOT NULL AUTO_INCREMENT,
  `nombres` varchar(60) NOT NULL DEFAULT '',
  `documento` varchar(15) NOT NULL DEFAULT '',
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `telefonos` varchar(20) NOT NULL DEFAULT '',
  `fechaalta` date NOT NULL DEFAULT '0000-00-00',
  `celular` varchar(20) NOT NULL DEFAULT '',
  `direccion` varchar(200) NOT NULL DEFAULT '',
  `email` varchar(100) NOT NULL DEFAULT '',
  `observacion` varchar(200) NOT NULL DEFAULT '',
  `comision` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `comisionfija` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio1` int(1) NOT NULL DEFAULT '0',
  `precio2` int(1) NOT NULL DEFAULT '0',
  `precio3` int(1) NOT NULL DEFAULT '0',
  `precio4` int(1) NOT NULL DEFAULT '0',
  `precio5` int(1) NOT NULL DEFAULT '0',
  `precio6` int(1) NOT NULL DEFAULT '0',
  `precio7` int(1) NOT NULL DEFAULT '0',
  `nlineno` int(11) NOT NULL DEFAULT '0',
  `idciudad` int(11) NOT NULL DEFAULT '0',
  `codigobarra` varchar(20) NOT NULL DEFAULT '',
  `foto` varchar(100) NOT NULL DEFAULT '',
  `fecha_suspendido` date NOT NULL DEFAULT '0000-00-00',
  `motivosuspendido` varchar(200) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `fechanac` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`idsupervisor`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tablasmetas`;
CREATE TABLE `tablasmetas` (
  `idtablameta` int(11) NOT NULL AUTO_INCREMENT,
  `desde` decimal(12,2) NOT NULL DEFAULT '0.00',
  `operacion` varchar(2) NOT NULL DEFAULT '',
  `hasta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `porcentaje` decimal(12,2) NOT NULL DEFAULT '0.00',
  `monto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `orden` int(1) NOT NULL DEFAULT '0',
  `calculacomision` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idtablameta`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tbl_historial`;
CREATE TABLE `tbl_historial` (
  `idhistorial` int(11) NOT NULL AUTO_INCREMENT,
  `fecha` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `comandosql` varchar(254) NOT NULL DEFAULT '',
  `segundos` char(20) NOT NULL DEFAULT '0',
  `suceso` tinyint(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idhistorial`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tbl_tranusu`;
CREATE TABLE `tbl_tranusu` (
  `id_tranusu` int(11) NOT NULL AUTO_INCREMENT,
  `form_tran` varchar(30) NOT NULL DEFAULT '',
  `tabla_tran` varchar(30) NOT NULL DEFAULT '',
  `tipo_tran` char(1) NOT NULL DEFAULT '',
  `fecha_tran` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `sql_tran` text NOT NULL,
  `clave_tran` int(11) NOT NULL DEFAULT '0',
  `userid_tran` varchar(30) NOT NULL DEFAULT '',
  PRIMARY KEY (`id_tranusu`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblciudad`;
CREATE TABLE `tblciudad` (
  `idciudad` int(11) NOT NULL DEFAULT '0',
  `desciudad` varchar(60) NOT NULL DEFAULT '',
  `idestado` int(11) NOT NULL DEFAULT '0'
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblclientes`;
CREATE TABLE `tblclientes` (
  `idcliente` int(11) NOT NULL AUTO_INCREMENT,
  `descliente` varchar(120) NOT NULL DEFAULT '',
  `rfc` char(13) NOT NULL DEFAULT '',
  `direccion` varchar(100) NOT NULL DEFAULT '',
  `idestado` int(11) NOT NULL DEFAULT '0',
  `idciudad` int(11) NOT NULL DEFAULT '0',
  `cp` decimal(5,0) NOT NULL DEFAULT '0',
  `colonia` varchar(50) NOT NULL DEFAULT '',
  `telefono` char(20) NOT NULL DEFAULT '',
  `email` varchar(40) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `userid` varchar(25) NOT NULL DEFAULT '',
  PRIMARY KEY (`idcliente`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblestados`;
CREATE TABLE `tblestados` (
  `idestado` int(11) NOT NULL DEFAULT '0',
  `desestado` varchar(50) NOT NULL DEFAULT '',
  `Abreviatura` varchar(5) NOT NULL DEFAULT ''
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblfacturas`;
CREATE TABLE `tblfacturas` (
  `idfactura` int(11) NOT NULL AUTO_INCREMENT,
  `nro_factura` decimal(10,0) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `fe_venta` date NOT NULL DEFAULT '0000-00-00',
  `entregar_a` char(50) NOT NULL DEFAULT '',
  `pre_venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `porc_descuento` decimal(10,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `iva` decimal(2,0) NOT NULL DEFAULT '0',
  `impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`idfactura`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblitemfacturas`;
CREATE TABLE `tblitemfacturas` (
  `iditem` int(11) NOT NULL AUTO_INCREMENT,
  `idfactura` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `cantidad` decimal(10,0) unsigned NOT NULL DEFAULT '0',
  `pre_unitario` decimal(10,2) NOT NULL DEFAULT '0.00',
  `importe` decimal(10,2) unsigned NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`iditem`),
  KEY `FK_tblitemfacturas` (`idfactura`),
  CONSTRAINT `tblitemfacturas_ibfk_1` FOREIGN KEY (`idfactura`) REFERENCES `tblfacturas` (`idfactura`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblproductos`;
CREATE TABLE `tblproductos` (
  `idproducto` int(11) NOT NULL AUTO_INCREMENT,
  `cve_producto` char(11) NOT NULL DEFAULT '',
  `desproducto` char(50) NOT NULL DEFAULT '',
  `stock_minimo` decimal(10,0) NOT NULL DEFAULT '0',
  `stock_maximo` decimal(10,0) NOT NULL DEFAULT '0',
  `precio_compra` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio_interno` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio_venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  PRIMARY KEY (`idproducto`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tblreportes`;
CREATE TABLE `tblreportes` (
  `Idreporte` int(10) NOT NULL AUTO_INCREMENT,
  `Nameform` varchar(40) NOT NULL DEFAULT '',
  `Namereport` varchar(40) NOT NULL DEFAULT '',
  `Descrireport` varchar(250) NOT NULL DEFAULT '',
  `Filtro` varchar(250) NOT NULL DEFAULT '',
  `Comando` varchar(50) NOT NULL DEFAULT '',
  PRIMARY KEY (`Idreporte`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `timbrado`;
CREATE TABLE `timbrado` (
  `idtimbrado` int(11) NOT NULL AUTO_INCREMENT,
  `idproveedor` int(11) NOT NULL DEFAULT '0',
  `desproveedor` varchar(150) NOT NULL DEFAULT '',
  `timbrado` varchar(25) NOT NULL DEFAULT '',
  PRIMARY KEY (`idtimbrado`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `timbradolocal`;
CREATE TABLE `timbradolocal` (
  `idtimbradolocal` int(11) NOT NULL AUTO_INCREMENT,
  `idsucursal` int(11) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `n1` int(5) NOT NULL DEFAULT '0',
  `n2` int(5) NOT NULL DEFAULT '0',
  `n3` int(10) NOT NULL DEFAULT '0',
  `factura` varchar(25) NOT NULL DEFAULT '',
  `cantidad` int(10) NOT NULL DEFAULT '0',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `fechainicio` date NOT NULL DEFAULT '0000-00-00',
  PRIMARY KEY (`idtimbradolocal`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `tipodocumentos`;
CREATE TABLE `tipodocumentos` (
  `idtipodoc` int(11) NOT NULL AUTO_INCREMENT,
  `codigo` varchar(3) NOT NULL DEFAULT '',
  `descripcion` varchar(30) NOT NULL DEFAULT '',
  PRIMARY KEY (`idtipodoc`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `trasladodepositos`;
CREATE TABLE `trasladodepositos` (
  `idtrasladodeposito` int(11) NOT NULL AUTO_INCREMENT,
  `iddepositoorigen` int(11) NOT NULL DEFAULT '0',
  `iddepositodestino` int(11) NOT NULL DEFAULT '0',
  `idproducto` int(11) NOT NULL DEFAULT '0',
  `codigo` varchar(20) NOT NULL DEFAULT '',
  `cantidad` decimal(12,2) NOT NULL DEFAULT '0.00',
  `observaciones` varchar(200) NOT NULL DEFAULT '',
  `autorizado` varchar(150) NOT NULL DEFAULT '',
  `fecha` date NOT NULL DEFAULT '0000-00-00',
  `hora` varchar(8) NOT NULL DEFAULT '',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  PRIMARY KEY (`idtrasladodeposito`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `usuarios`;
CREATE TABLE `usuarios` (
  `Nlineno` int(11) NOT NULL AUTO_INCREMENT,
  `Cidgrupo` varchar(10) NOT NULL DEFAULT '',
  `Cidusuario` varchar(10) NOT NULL DEFAULT '',
  `Cdescribe` varchar(50) NOT NULL DEFAULT '',
  `Cpassword` varchar(40) NOT NULL DEFAULT '',
  `Csuspendido` tinyint(1) NOT NULL DEFAULT '0',
  `Fecha` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `Userid` varchar(25) NOT NULL DEFAULT '',
  `noife` decimal(12,0) NOT NULL DEFAULT '0',
  `iddependencia` int(11) NOT NULL DEFAULT '0',
  `idturno` int(11) NOT NULL DEFAULT '0',
  `userweb` tinyint(1) NOT NULL DEFAULT '0',
  `claveweb` char(32) NOT NULL DEFAULT '',
  `idsucursal` int(1) NOT NULL DEFAULT '0',
  `tipovendedor` int(1) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(1) NOT NULL,
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`Nlineno`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `usuariosred`;
CREATE TABLE `usuariosred` (
  `id_usured` int(11) NOT NULL AUTO_INCREMENT,
  `nombre` varchar(70) NOT NULL DEFAULT '',
  `maquina` varchar(70) NOT NULL DEFAULT '',
  `ip` varchar(25) NOT NULL DEFAULT '',
  `usuario` varchar(70) NOT NULL DEFAULT '',
  `bloqueo` tinyint(1) unsigned NOT NULL DEFAULT '0',
  `inicio` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `fin` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `username` varchar(40) NOT NULL DEFAULT '',
  PRIMARY KEY (`id_usured`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `vendedores`;
CREATE TABLE `vendedores` (
  `idvendedor` int(10) NOT NULL AUTO_INCREMENT,
  `nombres` varchar(60) NOT NULL DEFAULT '',
  `documento` varchar(15) NOT NULL DEFAULT '',
  `ruc` varchar(20) NOT NULL DEFAULT '',
  `telefonos` varchar(20) NOT NULL DEFAULT '',
  `fechaalta` date NOT NULL DEFAULT '0000-00-00',
  `celular` varchar(20) NOT NULL DEFAULT '',
  `direccion` varchar(200) NOT NULL DEFAULT '',
  `email` varchar(100) NOT NULL DEFAULT '',
  `observacion` varchar(200) NOT NULL DEFAULT '',
  `comision` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `comisionfija` decimal(12,2) NOT NULL DEFAULT '0.00',
  `precio1` int(1) NOT NULL DEFAULT '0',
  `precio2` int(1) NOT NULL DEFAULT '0',
  `precio3` int(1) NOT NULL DEFAULT '0',
  `precio4` int(1) NOT NULL DEFAULT '0',
  `precio5` int(1) NOT NULL DEFAULT '0',
  `precio6` int(1) NOT NULL DEFAULT '0',
  `precio7` int(1) NOT NULL DEFAULT '0',
  `nlineno` int(11) NOT NULL DEFAULT '0',
  `idciudad` int(11) NOT NULL DEFAULT '0',
  `codigobarra` varchar(20) NOT NULL DEFAULT '',
  `foto` varchar(100) NOT NULL DEFAULT '',
  `fecha_suspendido` date NOT NULL DEFAULT '0000-00-00',
  `motivosuspendido` varchar(200) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `fechanac` date NOT NULL DEFAULT '0000-00-00',
  `idsupervisor` int(11) NOT NULL DEFAULT '0',
  `password` varchar(20) NOT NULL DEFAULT '',
  `tablametas` int(1) NOT NULL DEFAULT '0',
  `tipovendedor` int(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`idvendedor`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

DROP TABLE IF EXISTS `ventas`;
CREATE TABLE `ventas` (
  `idventa` int(11) NOT NULL AUTO_INCREMENT,
  `nro_venta` int(11) NOT NULL DEFAULT '0',
  `nro_factura` varchar(25) NOT NULL DEFAULT '',
  `fecha_venta` date NOT NULL DEFAULT '0000-00-00',
  `forma_pago` int(1) NOT NULL DEFAULT '0',
  `iddeposito` int(11) NOT NULL DEFAULT '0',
  `idcaja` int(11) NOT NULL DEFAULT '0',
  `idcliente` int(11) NOT NULL DEFAULT '0',
  `idvendedor` int(11) NOT NULL DEFAULT '0',
  `timbrado` varchar(20) NOT NULL DEFAULT '',
  `total_exenta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `subtotal` decimal(12,2) NOT NULL DEFAULT '0.00',
  `descuento` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_venta` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva5` decimal(12,2) NOT NULL DEFAULT '0.00',
  `liquidacion_iva10` decimal(12,2) NOT NULL DEFAULT '0.00',
  `total_impuesto` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cidusuario` varchar(20) NOT NULL DEFAULT '',
  `concepto` varchar(100) NOT NULL DEFAULT '',
  `estado` int(1) NOT NULL DEFAULT '0',
  `tipoboleta` int(1) NOT NULL DEFAULT '0',
  `nro_boleta` int(11) NOT NULL DEFAULT '0',
  `cot_dolar` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_real` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_peso` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_euro` decimal(12,2) NOT NULL DEFAULT '0.00',
  `cot_guarani` decimal(12,2) NOT NULL DEFAULT '0.00',
  `moneda_dolar` decimal(20,2) NOT NULL DEFAULT '0.00',
  `moneda_peso` decimal(20,2) NOT NULL DEFAULT '0.00',
  `moneda_euro` decimal(20,2) NOT NULL DEFAULT '0.00',
  `moneda_guarani` decimal(20,2) NOT NULL DEFAULT '0.00',
  `moneda_real` decimal(20,2) NOT NULL DEFAULT '0.00',
  `observacion` varchar(250) NOT NULL DEFAULT '',
  `vencimiento` date NOT NULL DEFAULT '0000-00-00',
  `idmoneda` int(5) NOT NULL DEFAULT '0',
  `tipo_venta` int(1) NOT NULL DEFAULT '0',
  `entrega_dolar` decimal(20,2) DEFAULT '0.00',
  `entrega_guarani` decimal(20,2) DEFAULT '0.00',
  `entrega_real` decimal(20,2) DEFAULT '0.00',
  `entrega_peso` decimal(20,2) DEFAULT '0.00',
  `entrega_euro` decimal(20,2) DEFAULT '0.00',
  `vuelto_guarani` decimal(20,2) DEFAULT '0.00',
  `vuelto_real` decimal(20,2) DEFAULT '0.00',
  `vuelto_peso` decimal(20,2) DEFAULT '0.00',
  `vuelto_dolar` decimal(20,2) DEFAULT '0.00',
  `cobro_caja` int(1) DEFAULT '0',
  `punto` int(1) DEFAULT '0',
  `hora_venta` varchar(8) DEFAULT '',
  `cierre` int(1) DEFAULT '0',
  `mesa` int(1) DEFAULT '0',
  `barra` int(1) DEFAULT '0',
  `idadherente` int(11) DEFAULT '0',
  `costo_delivery` decimal(12,2) DEFAULT '0.00',
  `terminal` varchar(60) DEFAULT '',
  `fechaentrega` date DEFAULT '0000-00-00',
  `horaentrega` varchar(8) DEFAULT '',
  `cuota` int(11) DEFAULT '0',
  `facturaporparte` int(1) DEFAULT '0',
  `idventarela` int(11) DEFAULT '0',
  `idsucursal` int(11) DEFAULT '0',
  PRIMARY KEY (`idventa`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE OR REPLACE VIEW `vw_ajustes` AS select `ajustes`.`nro_ajuste` AS `nro_ajuste`,`ajustes`.`autorizadopor` AS `autorizadopor`,`ajustes`.`fecha` AS `fecha`,`ajustes`.`cidusuario` AS `cidusuario`,`ajustes`.`idajuste` AS `idajuste`,`depositos`.`descripcion` AS `DesDeposito`,`ajustes`.`iddeposito` AS `iddeposito`,`ajustes`.`motivo` AS `motivo`,`ajustes`.`motivotexto` AS `motivotexto`,`ajustes`.`especificar` AS `especificar`,`ajustes`.`concepto` AS `concepto`,`ajustes`.`sobrante` AS `sobrante`,`ajustes`.`faltante` AS `faltante`,`ajustes`.`total_diferencia` AS `total_diferencia`,`ajustes`.`nlineno` AS `nlineno` from (`ajustes` join `depositos` on((`ajustes`.`iddeposito` = `depositos`.`iddeposito`)));

CREATE OR REPLACE VIEW `vw_ajustesimpresion` AS select `ajustes`.`idajuste` AS `idajuste`,`ajustes`.`nro_ajuste` AS `nro_ajuste`,`ajustes`.`fecha` AS `fecha`,`ajustes`.`iddeposito` AS `iddeposito`,`depositos`.`descripcion` AS `DesDeposito`,`depositos`.`responsable` AS `responsable`,`ajustes`.`autorizadopor` AS `autorizadopor`,`ajustes`.`motivo` AS `motivo`,`ajustes`.`motivotexto` AS `motivotexto`,`ajustes`.`especificar` AS `especificar`,`ajustes`.`concepto` AS `concepto`,`ajustes`.`sobrante` AS `sobrante`,`ajustes`.`faltante` AS `faltante`,`ajustes`.`total_diferencia` AS `total_diferencia`,`ajustes`.`cidusuario` AS `cidusuario`,`itemsajustes`.`idproducto` AS `idproducto`,`productos`.`descripcion` AS `Desproducto`,`itemsajustes`.`codigo` AS `codigo`,`itemsajustes`.`salida` AS `salida`,`itemsajustes`.`entrada` AS `entrada`,`itemsajustes`.`precio` AS `precio`,`itemsajustes`.`total` AS `total` from (((`ajustes` join `itemsajustes` on((`ajustes`.`idajuste` = `itemsajustes`.`idajuste`))) join `depositos` on((`ajustes`.`iddeposito` = `depositos`.`iddeposito`))) join `productos` on((`itemsajustes`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_buscarpedidoscompras` AS select `pedidos`.`idpedido` AS `idpedido`,`pedidos`.`nro_pedido` AS `nro_pedido`,`pedidos`.`nro_factura` AS `nro_factura`,`pedidos`.`fecha_pedido` AS `fecha_pedido`,`pedidos`.`forma_pago` AS `forma_pago`,`pedidos`.`iddeposito` AS `iddeposito`,`pedidos`.`idcaja` AS `idcaja`,`pedidos`.`idproveedor` AS `idproveedor`,`pedidos`.`timbrado` AS `timbrado`,`pedidos`.`total_exenta` AS `total_exenta`,`pedidos`.`total_iva5` AS `total_iva5`,`pedidos`.`total_iva10` AS `total_iva10`,`pedidos`.`subtotal` AS `subtotal`,`pedidos`.`descuento` AS `descuento`,`pedidos`.`total_pedido` AS `total_pedido`,`pedidos`.`liquidacion_iva5` AS `liquidacion_iva5`,`pedidos`.`liquidacion_iva10` AS `liquidacion_iva10`,`pedidos`.`total_impuesto` AS `total_impuesto`,`pedidos`.`cidusuario` AS `cidusuario`,`pedidos`.`concepto` AS `concepto`,`pedidos`.`estado` AS `estado`,`pedidos`.`observacion` AS `observacion`,`proveedores`.`descripcion` AS `DesProveedor`,`pedidos`.`facturado` AS `facturado` from (`pedidos` join `proveedores` on((`pedidos`.`idproveedor` = `proveedores`.`idproveedor`)));

CREATE OR REPLACE VIEW `vw_buscarpresupuestos` AS select `presupuestos`.`nro_presupuesto` AS `nro_presupuesto`,`presupuestos`.`nro_factura` AS `nro_factura`,`presupuestos`.`fecha_presupuesto` AS `fecha_presupuesto`,`clientes`.`nombres` AS `DesCliente`,`vendedores`.`nombres` AS `DesVendedor`,`presupuestos`.`total_presupuesto` AS `total_presupuesto`,`presupuestos`.`idpresupuesto` AS `idpresupuesto`,`presupuestos`.`estado` AS `estado` from ((`presupuestos` join `clientes` on((`presupuestos`.`idcliente` = `clientes`.`idcliente`))) join `vendedores` on((`presupuestos`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_buscarpreventas` AS select `preventas`.`nro_preventa` AS `nro_preventa`,`preventas`.`nro_factura` AS `nro_factura`,`preventas`.`fecha_preventa` AS `fecha_preventa`,`clientes`.`nombres` AS `DesCliente`,`vendedores`.`nombres` AS `DesVendedor`,`preventas`.`total_preventa` AS `total_preventa`,`preventas`.`idpreventa` AS `idpreventa`,`preventas`.`facturado` AS `facturado`,`preventas`.`estado` AS `estado` from ((`preventas` join `clientes` on((`preventas`.`idcliente` = `clientes`.`idcliente`))) join `vendedores` on((`preventas`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_cobro_caja` AS select `ventas`.`idventa` AS `idventa`,`ventas`.`nro_venta` AS `nro_venta`,`ventas`.`fecha_venta` AS `fecha_venta`,`clientes`.`nombres` AS `nombres`,`ventas`.`descuento` AS `descuento`,`ventas`.`total_venta` AS `total_venta`,if((`ventas`.`cobro_caja` = 0),'Pendiente','Cobrado') AS `Estado_Cobro`,`ventas`.`subtotal` AS `subtotal`,`ventas`.`cot_dolar` AS `cot_dolar`,`ventas`.`cot_real` AS `cot_real`,`ventas`.`cot_peso` AS `cot_peso`,`ventas`.`cobro_caja` AS `cobro_caja`,`ventas`.`idcaja` AS `idcaja`,`ventas`.`idmoneda` AS `idmoneda` from (`ventas` left join `clientes` on((`ventas`.`idcliente` = `clientes`.`idcliente`)));

CREATE OR REPLACE VIEW `vw_cobrosclientes` AS select `cobrocliente`.`idcobro` AS `idcobro`,`cobrocliente`.`idventa` AS `idventa`,`cobrocliente`.`nro_venta` AS `nro_venta`,`cobrocliente`.`nro_factura` AS `nro_factura`,`cobrocliente`.`vencimiento` AS `vencimiento`,`cobrocliente`.`cuota` AS `cuota`,`cobrocliente`.`fecha` AS `fecha`,`cobrocliente`.`dia` AS `dia`,`cobrocliente`.`idcliente` AS `idcliente`,`cobrocliente`.`cobro` AS `cobro`,`cobrocliente`.`entrega` AS `entrega`,`cobrocliente`.`fecha_cobro` AS `fecha_cobro`,`cobrocliente`.`forma_cobro` AS `forma_cobro`,`cobrocliente`.`mora` AS `mora`,`cobrocliente`.`idcaja` AS `idcaja`,`cobrocliente`.`obs` AS `obs`,`clientes`.`nombres` AS `DesCliente`,`clientes`.`ruc` AS `ruc`,`clientes`.`telefono` AS `telefono`,`clientes`.`celular` AS `celular`,`clientes`.`fax` AS `fax`,`clientes`.`email` AS `email`,`clientes`.`direccion` AS `direccion`,`clientes`.`refinanciacion` AS `refinanciacion`,`clientes`.`judicial1` AS `judicial1`,`clientes`.`inforconf` AS `inforconf`,`clientes`.`estado` AS `estado` from (`cobrocliente` left join `clientes` on((`cobrocliente`.`idcliente` = `clientes`.`idcliente`)));

CREATE OR REPLACE VIEW `vw_comanda` AS select `comanda`.`idcomanda` AS `idcomanda`,`comanda`.`idventa` AS `idventa`,`comanda`.`idvendedor` AS `idvendedor`,`comanda`.`mesa_nro` AS `mesa_nro`,`comanda`.`barra_nro` AS `barra_nro`,`comanda`.`hora` AS `hora`,`comanda`.`fecha` AS `fecha`,`comanda`.`plato` AS `plato`,`comanda`.`estado` AS `estado`,`comanda`.`orden` AS `orden`,`vendedores`.`nombres` AS `nombres`,`comanda`.`observaciones` AS `observaciones`,`comanda`.`cantidad` AS `cantidad`,`comanda`.`idproducto` AS `idproducto`,`productos`.`cocina` AS `cocina`,`productos`.`otros` AS `otros` from ((`comanda` join `vendedores` on((`comanda`.`idvendedor` = `vendedores`.`idvendedor`))) join `productos` on((`comanda`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_comision_por_proveedores` AS select `comision_por_proveedor`.`idcomision` AS `idcomision`,`comision_por_proveedor`.`idproveedor` AS `idproveedor`,`proveedores`.`descripcion` AS `descripcion`,`comision_por_proveedor`.`comision` AS `comision`,`comision_por_proveedor`.`idvendedor` AS `idvendedor`,`vendedores`.`nombres` AS `nombres` from ((`comision_por_proveedor` left join `proveedores` on((`comision_por_proveedor`.`idproveedor` = `proveedores`.`idproveedor`))) left join `vendedores` on((`comision_por_proveedor`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_comisionsupervisores` AS select `supervisores`.`documento` AS `documento`,`supervisores`.`nombres` AS `DesSupervisor`,`supervisores`.`comisionfija` AS `comisionfija`,`supervisores`.`comision` AS `comision`,`ventas`.`total_venta` AS `total_venta`,`ventas`.`fecha_venta` AS `fecha_venta`,`vendedores`.`idsupervisor` AS `idsupervisor` from ((`ventas` join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) join `supervisores` on((`vendedores`.`idsupervisor` = `supervisores`.`idsupervisor`))) where (`vendedores`.`estado` = 1) order by `vendedores`.`idsupervisor`;

CREATE OR REPLACE VIEW `vw_comisionvendedores` AS select `ventas`.`total_venta` AS `total_venta`,`ventas`.`fecha_venta` AS `fecha_venta`,`vendedores`.`nombres` AS `desvendedor`,`vendedores`.`documento` AS `documento`,`vendedores`.`comision` AS `comision`,`vendedores`.`comisionfija` AS `comisionfija`,`vendedores`.`idsupervisor` AS `idsupervisor`,`ventas`.`idvendedor` AS `idvendedor` from (`ventas` left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) where ((`vendedores`.`documento` is not null) and (`ventas`.`estado` = 1) and (`vendedores`.`estado` = 1));

CREATE OR REPLACE VIEW `vw_comprahistoricoimpresion` AS select `compras`.`nro_compra` AS `nro_compra`,`itemscompras`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`itemscompras`.`cantidad` AS `cantidad`,`itemscompras`.`pre_unitario` AS `pre_unitario`,`compras`.`idcompra` AS `idcompra`,(`itemscompras`.`cantidad` * `itemscompras`.`pre_unitario`) AS `importe`,`compras`.`fecha_compra` AS `fecha_compra`,`compras`.`estado` AS `estado` from ((`compras` join `itemscompras` on((`compras`.`idcompra` = `itemscompras`.`idcompra`))) join `productos` on((`itemscompras`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_compras` AS select `compras`.`nro_factura` AS `nro_factura`,`proveedores`.`descripcion` AS `DesProveedor`,`compras`.`fecha_compra` AS `fecha_compra`,`compras`.`total_exenta` AS `total_exenta`,`compras`.`total_iva5` AS `total_iva5`,`compras`.`total_iva10` AS `total_iva10`,`depositos`.`descripcion` AS `DesDeposito`,`compras`.`cidusuario` AS `cidusuario`,`compras`.`forma_pago` AS `forma_pago`,`compras`.`subtotal` AS `subtotal`,`compras`.`descuento` AS `descuento`,`compras`.`total_compra` AS `total_compra`,`compras`.`liquidacion_iva5` AS `liquidacion_iva5`,`compras`.`liquidacion_iva10` AS `liquidacion_iva10`,`compras`.`total_impuesto` AS `total_impuesto`,`compras`.`iddeposito` AS `iddeposito`,`compras`.`idcaja` AS `idcaja`,`compras`.`idproveedor` AS `idproveedor`,`compras`.`timbrado` AS `timbrado`,`compras`.`nro_compra` AS `nro_compra`,`compras`.`concepto` AS `concepto`,`compras`.`estado` AS `estado`,`compras`.`idcompra` AS `idcompra`,`cajas`.`descripcion` AS `Descaja`,`proveedores`.`ruc` AS `ruc`,`proveedores`.`direccion` AS `direccion`,`proveedores`.`telefono` AS `telefono`,`compras`.`observacion` AS `observacion`,`compras`.`vencimiento` AS `vencimiento`,`compras`.`idmoneda` AS `idmoneda`,`compras`.`nro_boleta` AS `nro_boleta`,`compras`.`tipoboleta` AS `tipoboleta` from (((`compras` join `proveedores` on((`compras`.`idproveedor` = `proveedores`.`idproveedor`))) join `depositos` on((`compras`.`iddeposito` = `depositos`.`iddeposito`))) join `cajas` on((`compras`.`idcaja` = `cajas`.`idcaja`)));

CREATE OR REPLACE VIEW `vw_compuesto` AS select `compuesto`.`idcompuesto` AS `idcompuesto`,`compuesto`.`idproductocompuesto` AS `idproductocompuesto`,`compuesto`.`idproducto` AS `idproducto`,`compuesto`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`compuesto`.`cantidad` AS `cantidad` from (`compuesto` join `productos` on((`compuesto`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_creditoclientes` AS select `creditocliente`.`idcredito` AS `idcredito`,`creditocliente`.`nro_venta` AS `nro_venta`,`creditocliente`.`nro_factura` AS `nro_factura`,if((`creditocliente`.`idventa` = 0),_utf8'Cuenta a Recibir',_utf8'Venta de Productos') AS `Descripcion`,`creditocliente`.`fecha` AS `fecha`,`creditocliente`.`cuota` AS `cuota`,`creditocliente`.`dia` AS `dia`,`creditocliente`.`vencimiento` AS `vencimiento`,`creditocliente`.`saldo` AS `saldo`,`creditocliente`.`entrega` AS `entrega`,`creditocliente`.`idcliente` AS `idcliente`,`creditocliente`.`total` AS `total`,`creditocliente`.`idventa` AS `idventa`,if((`creditocliente`.`vencimiento` < curdate()),(to_days(now()) - to_days(`creditocliente`.`vencimiento`)),0) AS `Mora` from `creditocliente`;

CREATE OR REPLACE VIEW `vw_cuentapagar` AS select `cuentapagar`.`idcuentapagar` AS `idcuentapagar`,`cuentapagar`.`fecha_carga` AS `fecha_carga`,`cuentapagar`.`fecha_proceso` AS `fecha_proceso`,`cuentapagar`.`nro_pagare` AS `nro_pagare`,`cuentapagar`.`nro_factura` AS `nro_factura`,`cuentapagar`.`idproveedor` AS `idproveedor`,`cuentapagar`.`total` AS `total`,`cuentapagar`.`entrega` AS `entrega`,`cuentapagar`.`saldo` AS `saldo`,`cuentapagar`.`vencimiento` AS `vencimiento`,`cuentapagar`.`cuota` AS `cuota`,`proveedores`.`ruc` AS `ruc`,`proveedores`.`descripcion` AS `DesProveedor` from (`cuentapagar` join `proveedores` on((`cuentapagar`.`idproveedor` = `proveedores`.`idproveedor`)));

CREATE OR REPLACE VIEW `vw_cuentarecibir` AS select `cuentarecibir`.`idcuentarecibir` AS `idcuentarecibir`,`cuentarecibir`.`fecha_carga` AS `fecha_carga`,`cuentarecibir`.`fecha_proceso` AS `fecha_proceso`,`cuentarecibir`.`nro_pagare` AS `nro_pagare`,`cuentarecibir`.`nro_factura` AS `nro_factura`,`clientes`.`nombres` AS `DesCliente`,`cuentarecibir`.`total` AS `total`,`cuentarecibir`.`entrega` AS `entrega`,`cuentarecibir`.`saldo` AS `saldo`,`cuentarecibir`.`vencimiento` AS `vencimiento`,`cuentarecibir`.`cuota` AS `cuota`,`cuentarecibir`.`idcliente` AS `idcliente`,`clientes`.`ruc` AS `ruc`,`clientes`.`fecha_nacimiento` AS `fecha_nacimiento` from (`cuentarecibir` left join `clientes` on((`cuentarecibir`.`idcliente` = `clientes`.`idcliente`)));

CREATE OR REPLACE VIEW `vw_depositos` AS select `depositos`.`iddeposito` AS `iddeposito`,`depositos`.`descripcion` AS `descripcion`,`depositos`.`responsable` AS `responsable`,`depositos`.`estado` AS `estado` from `depositos`;

CREATE OR REPLACE VIEW `vw_descuentopersonal` AS select `descuentopersonal`.`documento` AS `documento`,`legajos`.`apellidos` AS `apellidos`,`legajos`.`nombres` AS `nombres`,`descuentopersonal`.`fecha_descuento` AS `fecha_descuento`,`descuentopersonal`.`monto` AS `monto`,`descuentopersonal`.`iddescuentopersonal` AS `iddescuentopersonal`,`descuentopersonal`.`idlegajo` AS `idlegajo`,`descuentopersonal`.`cuota` AS `cuota`,`descuentopersonal`.`saldo` AS `saldo`,`descuentopersonal`.`iddescuento` AS `iddescuento`,`descuentopersonal`.`desdescuento` AS `desdescuento`,`descuentopersonal`.`estado` AS `estado`,`descuentopersonal`.`monto_cuota` AS `monto_cuota` from (`descuentopersonal` join `legajos` on((`descuentopersonal`.`idlegajo` = `legajos`.`idlegajo`)));

CREATE OR REPLACE VIEW `vw_extractocliente` AS select `creditocliente`.`idcliente` AS `idcliente`,`clientes`.`nombres` AS `DesCliente`,`clientes`.`ruc` AS `ruc`,`creditocliente`.`nro_venta` AS `nro_venta`,`creditocliente`.`nro_factura` AS `nro_factura`,`creditocliente`.`fecha` AS `fecha`,`creditocliente`.`vencimiento` AS `vencimiento`,`creditocliente`.`dia` AS `dia`,`creditocliente`.`cuota` AS `cuota`,`creditocliente`.`total` AS `total`,`creditocliente`.`entrega` AS `entrega`,`creditocliente`.`saldo` AS `saldo`,`creditocliente`.`idventa` AS `idventa`,`creditocliente`.`idcredito` AS `idcredito`,`clientes`.`email` AS `email`,`clientes`.`fax` AS `fax`,`clientes`.`celular` AS `celular`,`clientes`.`telefono` AS `telefono`,`clientes`.`direccion` AS `direccion`,`clientes`.`estado` AS `estado`,`clientes`.`inforconf` AS `inforconf`,`clientes`.`judicial1` AS `judicial1`,`clientes`.`refinanciacion` AS `refinanciacion` from (`creditocliente` left join `clientes` on((`creditocliente`.`idcliente` = `clientes`.`idcliente`)));

CREATE OR REPLACE VIEW `vw_extractoproveedor` AS select `compras`.`idproveedor` AS `idproveedor`,`proveedores`.`descripcion` AS `DesProveedor`,`proveedores`.`ruc` AS `ruc`,`proveedores`.`telefono` AS `telefono`,`proveedores`.`direccion` AS `direccion`,`compras`.`nro_compra` AS `nro_compra`,`compras`.`nro_factura` AS `nro_factura`,`compras`.`fecha_compra` AS `fecha`,`compras`.`vencimiento` AS `vencimiento`,`compras`.`total_compra` AS `total`,`pagoproveedor`.`entrega` AS `entrega`,`pagoproveedor`.`pago` AS `pago`,`pagoproveedor`.`idcompra` AS `idcompra` from ((`compras` left join `proveedores` on((`compras`.`idproveedor` = `proveedores`.`idproveedor`))) left join `pagoproveedor` on((`compras`.`nro_compra` = `pagoproveedor`.`nro_compra`)));

CREATE OR REPLACE VIEW `vw_facturas` AS (select `tblfacturas`.`idfactura` AS `idfactura`,`tblfacturas`.`nro_factura` AS `nro_factura`,`tblfacturas`.`idcliente` AS `idcliente`,`tblclientes`.`descliente` AS `descliente`,`tblclientes`.`rfc` AS `rfc`,`tblfacturas`.`fe_venta` AS `fe_venta`,`tblfacturas`.`total_venta` AS `total_venta` from (`tblfacturas` left join `tblclientes` on((`tblfacturas`.`idcliente` = `tblclientes`.`idcliente`))));

CREATE OR REPLACE VIEW `vw_ganacias_costo_promedio` AS select `ventas`.`fecha_venta` AS `fecha_venta`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `descripcion`,`productos`.`costomedio` AS `costomedio`,`itemsventas`.`cantidad` AS `cantidad`,(`productos`.`costomedio` * `itemsventas`.`cantidad`) AS `TotalCantCosto`,`itemsventas`.`pre_unitario` AS `pre_unitario`,(`itemsventas`.`pre_unitario` * `itemsventas`.`cantidad`) AS `TotalCantVenta`,((`itemsventas`.`pre_unitario` * `itemsventas`.`cantidad`) - (`productos`.`costomedio` * `itemsventas`.`cantidad`)) AS `Ganancia` from ((`ventas` left join `itemsventas` on((`ventas`.`idventa` = `itemsventas`.`idventa`))) left join `productos` on((`itemsventas`.`idproducto` = `productos`.`idproducto`))) where (`ventas`.`estado` = 1) order by `ventas`.`fecha_venta`,`productos`.`descripcion`;

CREATE OR REPLACE VIEW `vw_ganacias_costo_real` AS select `ventas`.`fecha_venta` AS `fecha_venta`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `descripcion`,`productos`.`costo1` AS `costo1`,`itemsventas`.`cantidad` AS `cantidad`,(`productos`.`costo1` * `itemsventas`.`cantidad`) AS `TotalCantCosto`,`itemsventas`.`pre_unitario` AS `pre_unitario`,(`itemsventas`.`pre_unitario` * `itemsventas`.`cantidad`) AS `TotalCantVenta`,((`itemsventas`.`pre_unitario` * `itemsventas`.`cantidad`) - (`productos`.`costo1` * `itemsventas`.`cantidad`)) AS `Ganancia` from ((`ventas` left join `itemsventas` on((`ventas`.`idventa` = `itemsventas`.`idventa`))) left join `productos` on((`itemsventas`.`idproducto` = `productos`.`idproducto`))) where (`ventas`.`estado` = 1) order by `ventas`.`fecha_venta`,`productos`.`descripcion`;

CREATE OR REPLACE VIEW `vw_graficocompraanual` AS select sum(`compras`.`total_compra`) AS `Valor`,monthname(`compras`.`fecha_compra`) AS `Rotulo` from `compras` group by monthname(`compras`.`fecha_compra`) order by monthname(`compras`.`fecha_compra`) desc;

CREATE OR REPLACE VIEW `vw_graficocomprasemanal` AS select sum(`compras`.`total_compra`) AS `Valor`,dayname(`compras`.`fecha_compra`) AS `Rotulo` from `compras` group by dayname(`compras`.`fecha_compra`) order by dayname(`compras`.`fecha_compra`) desc;

CREATE OR REPLACE VIEW `vw_items_fac` AS (select `tblitemfacturas`.`iditem` AS `iditem`,`tblitemfacturas`.`idfactura` AS `idfactura`,`tblitemfacturas`.`idproducto` AS `idproducto`,`tblproductos`.`cve_producto` AS `cve_producto`,`tblproductos`.`desproducto` AS `desproducto`,`tblitemfacturas`.`cantidad` AS `cantidad`,`tblitemfacturas`.`pre_unitario` AS `pre_unitario`,`tblitemfacturas`.`importe` AS `importe` from (`tblitemfacturas` left join `tblproductos` on((`tblproductos`.`idproducto` = `tblitemfacturas`.`idproducto`))));

CREATE OR REPLACE VIEW `vw_itemsajustes` AS select `itemsajustes`.`codigo` AS `codigo`,`itemsajustes`.`entrada` AS `entrada`,`itemsajustes`.`salida` AS `salida`,`productos`.`descripcion` AS `descripcion`,`itemsajustes`.`precio` AS `precio`,`itemsajustes`.`total` AS `total`,`itemsajustes`.`idproducto` AS `idproducto`,`itemsajustes`.`iditemajuste` AS `iditemajuste`,`itemsajustes`.`nro_ajuste` AS `nro_ajuste`,`itemsajustes`.`idajuste` AS `idajuste`,`itemsdepositos`.`cantidad` AS `STOCK` from ((`itemsajustes` join `productos` on((`itemsajustes`.`idproducto` = `productos`.`idproducto`))) join `itemsdepositos` on((`itemsajustes`.`idproducto` = `itemsdepositos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_itemscompras` AS select `itemscompras`.`iditemcompra` AS `iditemcompra`,`itemscompras`.`idcompra` AS `idcompra`,`itemscompras`.`nro_factura` AS `nro_factura`,`itemscompras`.`idproducto` AS `idproducto`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`itemscompras`.`cantidad` AS `cantidad`,`itemscompras`.`pre_unitario` AS `pre_unitario`,`itemscompras`.`pre_exenta` AS `pre_exenta`,`itemscompras`.`pre_iva5` AS `pre_iva5`,`itemscompras`.`pre_iva10` AS `pre_iva10`,`itemscompras`.`nro_compra` AS `nro_compra`,`productos`.`costomedio` AS `costomedio`,`productos`.`fechafingarantia` AS `fechafingarantia`,`productos`.`diasgarantias` AS `diasgarantias`,`productos`.`fechainiciogarantia` AS `fechainiciogarantia`,`productos`.`controlagarantia` AS `controlagarantia`,`compras`.`iddeposito` AS `iddeposito`,`productos`.`compuesto` AS `compuesto`,`compras`.`forma_pago` AS `forma_pago`,`productos`.`imagen` AS `imagen`,`productos`.`iva` AS `iva` from ((`itemscompras` left join `productos` on((`itemscompras`.`idproducto` = `productos`.`idproducto`))) left join `compras` on((`itemscompras`.`idcompra` = `compras`.`idcompra`)));

CREATE OR REPLACE VIEW `vw_itemsdepositos` AS select `itemsdepositos`.`iditemdeposito` AS `iditemdeposito`,`itemsdepositos`.`iddeposito` AS `iddeposito`,`itemsdepositos`.`codigo` AS `codigo`,`depositos`.`descripcion` AS `descripcion`,`itemsdepositos`.`cantidad` AS `cantidad`,`itemsdepositos`.`idproducto` AS `idproducto` from (`itemsdepositos` join `depositos` on((`itemsdepositos`.`iddeposito` = `depositos`.`iddeposito`)));

CREATE OR REPLACE VIEW `vw_itemsdevolucioncompras` AS select `itemsdevolucioncompras`.`iditemdevolucioncompra` AS `iditemdevolucioncompra`,`itemsdevolucioncompras`.`iddevolucioncompra` AS `iddevolucioncompra`,`itemsdevolucioncompras`.`nro_factura` AS `nro_factura`,`itemsdevolucioncompras`.`nro_compra` AS `nro_compra`,`itemsdevolucioncompras`.`idproducto` AS `idproducto`,`itemsdevolucioncompras`.`codigo` AS `codigo`,`itemsdevolucioncompras`.`cantidad` AS `cantidad`,`itemsdevolucioncompras`.`pre_unitario` AS `pre_unitario`,`itemsdevolucioncompras`.`pre_exenta` AS `pre_exenta`,`itemsdevolucioncompras`.`pre_iva5` AS `pre_iva5`,`itemsdevolucioncompras`.`pre_iva10` AS `pre_iva10`,`itemsdevolucioncompras`.`controlagarantia` AS `controlagarantia`,`itemsdevolucioncompras`.`fechainiciogarantia` AS `fechainiciogarantia`,`itemsdevolucioncompras`.`diasgarantias` AS `diasgarantias`,`itemsdevolucioncompras`.`fechafingarantia` AS `fechafingarantia`,`productos`.`descripcion` AS `DesProducto`,`productos`.`idmoneda` AS `idmoneda`,`productos`.`cambio` AS `cambio`,`productos`.`costomedio` AS `costomedio`,`productos`.`costo1` AS `costo1`,`productos`.`p1` AS `p1`,`productos`.`precio1` AS `precio1`,`productos`.`idproveedor` AS `idproveedor`,`productos`.`referencia` AS `referencia`,`productos`.`idgrupoproducto` AS `idgrupoproducto`,`productos`.`idmarcaproducto` AS `idmarcaproducto`,`productos`.`familia` AS `familia`,`productos`.`aplicacion` AS `aplicacion`,`productos`.`stockminimo` AS `stockminimo`,`productos`.`stockmaximo` AS `stockmaximo`,`productos`.`idsubgrupoproducto` AS `idsubgrupoproducto`,`productos`.`idmodeloproducto` AS `idmodeloproducto`,`productos`.`observacion` AS `observacion`,`productos`.`costo2` AS `costo2`,`productos`.`p2` AS `p2`,`productos`.`precio2` AS `precio2`,`productos`.`costo3` AS `costo3`,`productos`.`p3` AS `p3`,`productos`.`precio3` AS `precio3`,`productos`.`costo4` AS `costo4`,`productos`.`p4` AS `p4`,`productos`.`precio4` AS `precio4`,`productos`.`costo5` AS `costo5`,`productos`.`p5` AS `p5`,`productos`.`precio5` AS `precio5`,`productos`.`costo6` AS `costo6`,`productos`.`p6` AS `p6`,`productos`.`precio6` AS `precio6`,`productos`.`costo7` AS `costo7`,`productos`.`p7` AS `p7`,`productos`.`precio7` AS `precio7`,`productos`.`estado` AS `estado`,`productos`.`iva` AS `iva`,`productos`.`nocontrolastock` AS `nocontrolastock`,`productos`.`usaserial` AS `usaserial`,`productos`.`imprimefactura` AS `imprimefactura`,`productos`.`autocantidad` AS `autocantidad`,`productos`.`autoprecio` AS `autoprecio`,`productos`.`usavencimiento` AS `usavencimiento`,`productos`.`descripcioneditable` AS `descripcioneditable`,`productos`.`vendesinstock` AS `vendesinstock`,`productos`.`autosuma` AS `autosuma`,`productos`.`publicarweb` AS `publicarweb`,`productos`.`fraccionable` AS `fraccionable`,`productos`.`compuesto` AS `compuesto`,`productos`.`codigobarra` AS `codigobarra`,`productos`.`impuesto` AS `impuesto`,`productos`.`imagen` AS `imagen`,`productos`.`fechaultimacompra` AS `fechaultimacompra`,`productos`.`cantidadultimacompra` AS `cantidadultimacompra`,`productos`.`costomonedaextranjera` AS `costomonedaextranjera`,`devolucioncompras`.`iddeposito` AS `iddeposito`,`productos`.`fechaultimaventa` AS `fechaultimaventa`,`productos`.`cantidadultimaventa` AS `cantidadultimaventa` from ((`itemsdevolucioncompras` left join `devolucioncompras` on((`itemsdevolucioncompras`.`iddevolucioncompra` = `devolucioncompras`.`iddevolucioncompra`))) left join `productos` on((`itemsdevolucioncompras`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_itemsdevolucionventas` AS select sql_no_cache `itemsdevolucionventas`.`iditemdevolucionventa` AS `iditemdevolucionventa`,`itemsdevolucionventas`.`iddevolucionventa` AS `iddevolucionventa`,`itemsdevolucionventas`.`nro_factura` AS `nro_factura`,`itemsdevolucionventas`.`nro_venta` AS `nro_venta`,`itemsdevolucionventas`.`idproducto` AS `idproducto`,`itemsdevolucionventas`.`codigo` AS `codigo`,`itemsdevolucionventas`.`cantidad` AS `cantidad`,`itemsdevolucionventas`.`pre_unitario` AS `pre_unitario`,`itemsdevolucionventas`.`pre_exenta` AS `pre_exenta`,`itemsdevolucionventas`.`pre_iva5` AS `pre_iva5`,`itemsdevolucionventas`.`pre_iva10` AS `pre_iva10`,`itemsdevolucionventas`.`controlagarantia` AS `controlagarantia`,`itemsdevolucionventas`.`fechainiciogarantia` AS `fechainiciogarantia`,`itemsdevolucionventas`.`diasgarantias` AS `diasgarantias`,`itemsdevolucionventas`.`fechafingarantia` AS `fechafingarantia`,`productos`.`descripcion` AS `DesProducto`,`productos`.`idmoneda` AS `idmoneda`,`productos`.`cambio` AS `cambio`,`productos`.`costomedio` AS `costomedio`,`productos`.`costo1` AS `costo1`,`productos`.`p1` AS `p1`,`productos`.`precio1` AS `precio1`,`productos`.`idproveedor` AS `idproveedor`,`productos`.`referencia` AS `referencia`,`productos`.`idgrupoproducto` AS `idgrupoproducto`,`productos`.`idmarcaproducto` AS `idmarcaproducto`,`productos`.`familia` AS `familia`,`productos`.`aplicacion` AS `aplicacion`,`productos`.`stockminimo` AS `stockminimo`,`productos`.`stockmaximo` AS `stockmaximo`,`productos`.`idsubgrupoproducto` AS `idsubgrupoproducto`,`productos`.`idmodeloproducto` AS `idmodeloproducto`,`productos`.`observacion` AS `observacion`,`productos`.`costo2` AS `costo2`,`productos`.`p2` AS `p2`,`productos`.`precio2` AS `precio2`,`productos`.`costo3` AS `costo3`,`productos`.`p3` AS `p3`,`productos`.`precio3` AS `precio3`,`productos`.`costo4` AS `costo4`,`productos`.`p4` AS `p4`,`productos`.`precio4` AS `precio4`,`productos`.`costo5` AS `costo5`,`productos`.`p5` AS `p5`,`productos`.`precio5` AS `precio5`,`productos`.`costo6` AS `costo6`,`productos`.`p6` AS `p6`,`productos`.`precio6` AS `precio6`,`productos`.`costo7` AS `costo7`,`productos`.`p7` AS `p7`,`productos`.`precio7` AS `precio7`,`productos`.`estado` AS `estado`,`productos`.`iva` AS `iva`,`productos`.`nocontrolastock` AS `nocontrolastock`,`productos`.`usaserial` AS `usaserial`,`productos`.`imprimefactura` AS `imprimefactura`,`productos`.`autocantidad` AS `autocantidad`,`productos`.`autoprecio` AS `autoprecio`,`productos`.`usavencimiento` AS `usavencimiento`,`productos`.`descripcioneditable` AS `descripcioneditable`,`productos`.`vendesinstock` AS `vendesinstock`,`productos`.`autosuma` AS `autosuma`,`productos`.`publicarweb` AS `publicarweb`,`productos`.`fraccionable` AS `fraccionable`,`productos`.`compuesto` AS `compuesto`,`productos`.`codigobarra` AS `codigobarra`,`productos`.`impuesto` AS `impuesto`,`productos`.`imagen` AS `imagen`,`productos`.`fechaultimacompra` AS `fechaultimacompra`,`productos`.`cantidadultimacompra` AS `cantidadultimacompra`,`productos`.`fechaultimaventa` AS `fechaultimaventa`,`productos`.`cantidadultimaventa` AS `cantidadultimaventa`,`productos`.`costomonedaextranjera` AS `costomonedaextranjera`,`devolucionventas`.`iddeposito` AS `iddeposito` from ((`itemsdevolucionventas` join `devolucionventas` on((`itemsdevolucionventas`.`iddevolucionventa` = `devolucionventas`.`iddevolucionventa`))) join `productos` on((`itemsdevolucionventas`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_itemspedidos` AS select `itemspedidos`.`iditempedido` AS `iditempedido`,`itemspedidos`.`idpedido` AS `idpedido`,`itemspedidos`.`nro_factura` AS `nro_factura`,`itemspedidos`.`idproducto` AS `idproducto`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`itemspedidos`.`cantidad` AS `cantidad`,`itemspedidos`.`pre_unitario` AS `pre_unitario`,`itemspedidos`.`pre_exenta` AS `pre_exenta`,`itemspedidos`.`pre_iva5` AS `pre_iva5`,`itemspedidos`.`pre_iva10` AS `pre_iva10`,`itemspedidos`.`nro_pedido` AS `nro_pedido`,`productos`.`costomedio` AS `costomedio` from (`itemspedidos` left join `productos` on((`itemspedidos`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_itemspresupuestos` AS select `itemspresupuestos`.`iditempresupuesto` AS `iditempresupuesto`,`itemspresupuestos`.`idpresupuesto` AS `idpresupuesto`,`itemspresupuestos`.`nro_factura` AS `nro_factura`,`itemspresupuestos`.`nro_presupuesto` AS `nro_presupuesto`,`itemspresupuestos`.`idproducto` AS `idproducto`,`itemspresupuestos`.`codigo` AS `codigo`,`itemspresupuestos`.`cantidad` AS `cantidad`,`itemspresupuestos`.`pre_unitario` AS `pre_unitario`,`itemspresupuestos`.`pre_exenta` AS `pre_exenta`,`itemspresupuestos`.`pre_iva5` AS `pre_iva5`,`itemspresupuestos`.`pre_iva10` AS `pre_iva10`,`productos`.`descripcion` AS `DesProducto`,`productos`.`fechafingarantia` AS `fechafingarantia`,`productos`.`diasgarantias` AS `diasgarantias`,`productos`.`fechainiciogarantia` AS `fechainiciogarantia`,`productos`.`controlagarantia` AS `controlagarantia`,`productos`.`costo1` AS `costo1`,`productos`.`costomedio` AS `costomedio`,`productos`.`idmoneda` AS `idmoneda`,`productos`.`cambio` AS `cambio`,`productos`.`p1` AS `p1`,`productos`.`precio1` AS `precio1`,`productos`.`idproveedor` AS `idproveedor`,`productos`.`referencia` AS `referencia`,`productos`.`idgrupoproducto` AS `idgrupoproducto`,`productos`.`idmarcaproducto` AS `idmarcaproducto`,`productos`.`familia` AS `familia`,`productos`.`aplicacion` AS `aplicacion`,`productos`.`stockminimo` AS `stockminimo`,`productos`.`stockmaximo` AS `stockmaximo`,`productos`.`idsubgrupoproducto` AS `idsubgrupoproducto`,`productos`.`idmodeloproducto` AS `idmodeloproducto`,`productos`.`observacion` AS `observacion`,`productos`.`costo2` AS `costo2`,`productos`.`p2` AS `p2`,`productos`.`precio2` AS `precio2`,`productos`.`costo3` AS `costo3`,`productos`.`p3` AS `p3`,`productos`.`precio3` AS `precio3`,`productos`.`costo4` AS `costo4`,`productos`.`p4` AS `p4`,`productos`.`precio4` AS `precio4`,`productos`.`costo5` AS `costo5`,`productos`.`p5` AS `p5`,`productos`.`precio5` AS `precio5`,`productos`.`costo6` AS `costo6`,`productos`.`p6` AS `p6`,`productos`.`precio6` AS `precio6`,`productos`.`costo7` AS `costo7`,`productos`.`p7` AS `p7`,`productos`.`precio7` AS `precio7`,`productos`.`estado` AS `estado`,`productos`.`iva` AS `iva`,`productos`.`nocontrolastock` AS `nocontrolastock`,`productos`.`usaserial` AS `usaserial`,`productos`.`imprimefactura` AS `imprimefactura`,`productos`.`autocantidad` AS `autocantidad`,`productos`.`autoprecio` AS `autoprecio`,`productos`.`usavencimiento` AS `usavencimiento`,`productos`.`descripcioneditable` AS `descripcioneditable`,`productos`.`vendesinstock` AS `vendesinstock`,`productos`.`autosuma` AS `autosuma`,`productos`.`publicarweb` AS `publicarweb`,`productos`.`fraccionable` AS `fraccionable`,`productos`.`compuesto` AS `compuesto`,`productos`.`codigobarra` AS `codigobarra`,`productos`.`impuesto` AS `impuesto`,`productos`.`imagen` AS `imagen`,`productos`.`fechaultimacompra` AS `fechaultimacompra`,`productos`.`cantidadultimacompra` AS `cantidadultimacompra`,`productos`.`costomonedaextranjera` AS `costomonedaextranjera`,`productos`.`cantidadultimaventa` AS `cantidadultimaventa`,`productos`.`fechaultimaventa` AS `fechaultimaventa` from (`itemspresupuestos` left join `productos` on((`itemspresupuestos`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_itemspreventas` AS select `itemspreventas`.`idpreventa` AS `idpreventa`,`itemspreventas`.`nro_factura` AS `nro_factura`,`itemspreventas`.`nro_preventa` AS `nro_preventa`,`itemspreventas`.`idproducto` AS `idproducto`,`itemspreventas`.`codigo` AS `codigo`,`itemspreventas`.`cantidad` AS `cantidad`,`itemspreventas`.`pre_unitario` AS `pre_unitario`,`itemspreventas`.`pre_exenta` AS `pre_exenta`,`itemspreventas`.`pre_iva5` AS `pre_iva5`,`itemspreventas`.`pre_iva10` AS `pre_iva10`,`productos`.`descripcion` AS `DesProducto`,`productos`.`fechafingarantia` AS `fechafingarantia`,`productos`.`diasgarantias` AS `diasgarantias`,`productos`.`fechainiciogarantia` AS `fechainiciogarantia`,`productos`.`controlagarantia` AS `controlagarantia`,`productos`.`costo1` AS `costo1`,`productos`.`costomedio` AS `costomedio`,`productos`.`idmoneda` AS `idmoneda`,`productos`.`cambio` AS `cambio`,`productos`.`p1` AS `p1`,`productos`.`precio1` AS `precio1`,`productos`.`idproveedor` AS `idproveedor`,`productos`.`referencia` AS `referencia`,`productos`.`idgrupoproducto` AS `idgrupoproducto`,`productos`.`idmarcaproducto` AS `idmarcaproducto`,`productos`.`familia` AS `familia`,`productos`.`aplicacion` AS `aplicacion`,`productos`.`stockminimo` AS `stockminimo`,`productos`.`stockmaximo` AS `stockmaximo`,`productos`.`idsubgrupoproducto` AS `idsubgrupoproducto`,`productos`.`idmodeloproducto` AS `idmodeloproducto`,`productos`.`observacion` AS `observacion`,`productos`.`costo2` AS `costo2`,`productos`.`p2` AS `p2`,`productos`.`precio2` AS `precio2`,`productos`.`costo3` AS `costo3`,`productos`.`p3` AS `p3`,`productos`.`precio3` AS `precio3`,`productos`.`costo4` AS `costo4`,`productos`.`p4` AS `p4`,`productos`.`precio4` AS `precio4`,`productos`.`costo5` AS `costo5`,`productos`.`p5` AS `p5`,`productos`.`precio5` AS `precio5`,`productos`.`costo6` AS `costo6`,`productos`.`p6` AS `p6`,`productos`.`precio6` AS `precio6`,`productos`.`costo7` AS `costo7`,`productos`.`p7` AS `p7`,`productos`.`precio7` AS `precio7`,`productos`.`estado` AS `estado`,`productos`.`iva` AS `iva`,`productos`.`nocontrolastock` AS `nocontrolastock`,`productos`.`usaserial` AS `usaserial`,`productos`.`imprimefactura` AS `imprimefactura`,`productos`.`autocantidad` AS `autocantidad`,`productos`.`autoprecio` AS `autoprecio`,`productos`.`usavencimiento` AS `usavencimiento`,`productos`.`descripcioneditable` AS `descripcioneditable`,`productos`.`vendesinstock` AS `vendesinstock`,`productos`.`autosuma` AS `autosuma`,`productos`.`publicarweb` AS `publicarweb`,`productos`.`fraccionable` AS `fraccionable`,`productos`.`compuesto` AS `compuesto`,`productos`.`codigobarra` AS `codigobarra`,`productos`.`impuesto` AS `impuesto`,`productos`.`imagen` AS `imagen`,`productos`.`fechaultimacompra` AS `fechaultimacompra`,`productos`.`cantidadultimacompra` AS `cantidadultimacompra`,`productos`.`costomonedaextranjera` AS `costomonedaextranjera`,`itemspreventas`.`iditempreventa` AS `iditempreventa`,`productos`.`cantidadultimaventa` AS `cantidadultimaventa`,`productos`.`fechaultimaventa` AS `fechaultimaventa` from (`itemspreventas` left join `productos` on((`itemspreventas`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_itemsventas` AS select `itemsventas`.`iditemventa` AS `iditemventa`,`itemsventas`.`idventa` AS `idventa`,`itemsventas`.`nro_factura` AS `nro_factura`,`itemsventas`.`nro_venta` AS `nro_venta`,`itemsventas`.`idproducto` AS `idproducto`,`itemsventas`.`codigo` AS `codigo`,`itemsventas`.`cantidad` AS `cantidad`,`itemsventas`.`pre_unitario` AS `pre_unitario`,`itemsventas`.`pre_exenta` AS `pre_exenta`,`itemsventas`.`pre_iva5` AS `pre_iva5`,`itemsventas`.`pre_iva10` AS `pre_iva10`,`productos`.`descripcion` AS `DesProducto`,`productos`.`fechafingarantia` AS `fechafingarantia`,`productos`.`diasgarantias` AS `diasgarantias`,`productos`.`fechainiciogarantia` AS `fechainiciogarantia`,`productos`.`controlagarantia` AS `controlagarantia`,`productos`.`costo1` AS `costo1`,`productos`.`costomedio` AS `costomedio`,`productos`.`idmoneda` AS `idmoneda`,`productos`.`cambio` AS `cambio`,`productos`.`p1` AS `p1`,`productos`.`precio1` AS `precio1`,`productos`.`idproveedor` AS `idproveedor`,`productos`.`referencia` AS `referencia`,`productos`.`idgrupoproducto` AS `idgrupoproducto`,`productos`.`idmarcaproducto` AS `idmarcaproducto`,`productos`.`familia` AS `familia`,`productos`.`aplicacion` AS `aplicacion`,`productos`.`stockminimo` AS `stockminimo`,`productos`.`stockmaximo` AS `stockmaximo`,`productos`.`idsubgrupoproducto` AS `idsubgrupoproducto`,`productos`.`idmodeloproducto` AS `idmodeloproducto`,`productos`.`observacion` AS `observacion`,`productos`.`costo2` AS `costo2`,`productos`.`p2` AS `p2`,`productos`.`precio2` AS `precio2`,`productos`.`costo3` AS `costo3`,`productos`.`p3` AS `p3`,`productos`.`precio3` AS `precio3`,`productos`.`costo4` AS `costo4`,`productos`.`p4` AS `p4`,`productos`.`precio4` AS `precio4`,`productos`.`costo5` AS `costo5`,`productos`.`p5` AS `p5`,`productos`.`precio5` AS `precio5`,`productos`.`costo6` AS `costo6`,`productos`.`p6` AS `p6`,`productos`.`precio6` AS `precio6`,`productos`.`costo7` AS `costo7`,`productos`.`p7` AS `p7`,`productos`.`precio7` AS `precio7`,`productos`.`estado` AS `estado`,`productos`.`iva` AS `iva`,`productos`.`nocontrolastock` AS `nocontrolastock`,`productos`.`usaserial` AS `usaserial`,`productos`.`imprimefactura` AS `imprimefactura`,`productos`.`autocantidad` AS `autocantidad`,`productos`.`autoprecio` AS `autoprecio`,`productos`.`usavencimiento` AS `usavencimiento`,`productos`.`descripcioneditable` AS `descripcioneditable`,`productos`.`vendesinstock` AS `vendesinstock`,`productos`.`autosuma` AS `autosuma`,`productos`.`publicarweb` AS `publicarweb`,`productos`.`fraccionable` AS `fraccionable`,`productos`.`compuesto` AS `compuesto`,`productos`.`codigobarra` AS `codigobarra`,`productos`.`impuesto` AS `impuesto`,`productos`.`imagen` AS `imagen`,`productos`.`fechaultimacompra` AS `fechaultimacompra`,`productos`.`cantidadultimacompra` AS `cantidadultimacompra`,`productos`.`fechaultimaventa` AS `fechaultimaventa`,`productos`.`cantidadultimaventa` AS `cantidadultimaventa`,`productos`.`costomonedaextranjera` AS `costomonedaextranjera`,`ventas`.`iddeposito` AS `iddeposito`,`productos`.`comanda` AS `comanda`,`productos`.`menu` AS `menu`,`productos`.`guarnicion` AS `guarnicion`,`productos`.`muestrabusqueda` AS `muestrabusqueda`,`itemsventas`.`impresora` AS `impresora`,`itemsventas`.`imprimido` AS `imprimido`,`itemsventas`.`facturado` AS `facturado` from ((`itemsventas` left join `productos` on((`itemsventas`.`idproducto` = `productos`.`idproducto`))) left join `ventas` on((`itemsventas`.`idventa` = `ventas`.`idventa`)));

CREATE OR REPLACE VIEW `vw_itemsventas_depositos` AS select `itemsventas`.`iditemventa` AS `iditemventa`,`itemsventas`.`idventa` AS `idventa`,`itemsventas`.`nro_factura` AS `nro_factura`,`itemsventas`.`nro_venta` AS `nro_venta`,`itemsventas`.`idproducto` AS `idproducto`,`itemsventas`.`codigo` AS `codigo`,`itemsventas`.`cantidad` AS `cantidad`,`itemsventas`.`pre_unitario` AS `pre_unitario`,`itemsventas`.`pre_exenta` AS `pre_exenta`,`itemsventas`.`pre_iva5` AS `pre_iva5`,`itemsventas`.`pre_iva10` AS `pre_iva10`,`productos`.`descripcion` AS `DesProducto`,`productos`.`fechafingarantia` AS `fechafingarantia`,`productos`.`diasgarantias` AS `diasgarantias`,`productos`.`fechainiciogarantia` AS `fechainiciogarantia`,`productos`.`controlagarantia` AS `controlagarantia`,`productos`.`costo1` AS `costo1`,`productos`.`costomedio` AS `costomedio`,`productos`.`idmoneda` AS `idmoneda`,`productos`.`cambio` AS `cambio`,`productos`.`p1` AS `p1`,`productos`.`precio1` AS `precio1`,`productos`.`idproveedor` AS `idproveedor`,`productos`.`referencia` AS `referencia`,`productos`.`idgrupoproducto` AS `idgrupoproducto`,`productos`.`idmarcaproducto` AS `idmarcaproducto`,`productos`.`familia` AS `familia`,`productos`.`aplicacion` AS `aplicacion`,`productos`.`stockminimo` AS `stockminimo`,`productos`.`stockmaximo` AS `stockmaximo`,`productos`.`idsubgrupoproducto` AS `idsubgrupoproducto`,`productos`.`idmodeloproducto` AS `idmodeloproducto`,`productos`.`observacion` AS `observacion`,`productos`.`costo2` AS `costo2`,`productos`.`p2` AS `p2`,`productos`.`precio2` AS `precio2`,`productos`.`costo3` AS `costo3`,`productos`.`p3` AS `p3`,`productos`.`precio3` AS `precio3`,`productos`.`costo4` AS `costo4`,`productos`.`p4` AS `p4`,`productos`.`precio4` AS `precio4`,`productos`.`costo5` AS `costo5`,`productos`.`p5` AS `p5`,`productos`.`precio5` AS `precio5`,`productos`.`costo6` AS `costo6`,`productos`.`p6` AS `p6`,`productos`.`precio6` AS `precio6`,`productos`.`costo7` AS `costo7`,`productos`.`p7` AS `p7`,`productos`.`precio7` AS `precio7`,`productos`.`estado` AS `estado`,`productos`.`iva` AS `iva`,`productos`.`nocontrolastock` AS `nocontrolastock`,`productos`.`usaserial` AS `usaserial`,`productos`.`imprimefactura` AS `imprimefactura`,`productos`.`autocantidad` AS `autocantidad`,`productos`.`autoprecio` AS `autoprecio`,`productos`.`usavencimiento` AS `usavencimiento`,`productos`.`descripcioneditable` AS `descripcioneditable`,`productos`.`vendesinstock` AS `vendesinstock`,`productos`.`autosuma` AS `autosuma`,`productos`.`publicarweb` AS `publicarweb`,`productos`.`fraccionable` AS `fraccionable`,`productos`.`compuesto` AS `compuesto`,`productos`.`codigobarra` AS `codigobarra`,`productos`.`impuesto` AS `impuesto`,`productos`.`imagen` AS `imagen`,`productos`.`fechaultimacompra` AS `fechaultimacompra`,`productos`.`cantidadultimacompra` AS `cantidadultimacompra`,`productos`.`fechaultimaventa` AS `fechaultimaventa`,`productos`.`cantidadultimaventa` AS `cantidadultimaventa`,`productos`.`costomonedaextranjera` AS `costomonedaextranjera`,`itemsventas`.`iddeposito` AS `iddeposito`,`depositos`.`descripcion` AS `desdeposito` from (((`itemsventas` left join `productos` on((`itemsventas`.`idproducto` = `productos`.`idproducto`))) left join `ventas` on((`itemsventas`.`idventa` = `ventas`.`idventa`))) left join `depositos` on((`itemsventas`.`iddeposito` = `depositos`.`iddeposito`)));

CREATE OR REPLACE VIEW `vw_lote_productos` AS select `productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `descripcion`,`lote_producto`.`lote` AS `lote`,`lote_producto`.`vencimiento` AS `vencimiento`,`lote_producto`.`cantidad` AS `cantidad`,`lote_producto`.`idproducto` AS `idproducto` from (`lote_producto` left join `productos` on((`lote_producto`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_movimientocaja` AS select `movimientocaja`.`idmovimientocaja` AS `idmovimientocaja`,`movimientocaja`.`fechaalta` AS `fechaalta`,`movimientocaja`.`detalle` AS `detalle`,`movimientocaja`.`tipo` AS `tipo`,`movimientocaja`.`ingresos` AS `ingresos`,`movimientocaja`.`egresos` AS `egresos`,`movimientocaja`.`idcaja` AS `idcaja`,`cajas`.`descripcion` AS `descripcion`,`rubros`.`nombrerubro` AS `nombrerubro`,`rubros`.`tipocuenta` AS `tipocuenta`,`rubros`.`proceso` AS `proceso`,`rubros`.`idrubros` AS `idrubros`,`rubros`.`nrorubro` AS `nrorubro`,`movimientocaja`.`idmoneda` AS `idmoneda`,`movimientocaja`.`cidusuario` AS `cidusuario`,`monedas`.`descripcion` AS `DesMoneda`,`movimientocaja`.`hora` AS `hora`,`ventas`.`estado` AS `estado`,`movimientocaja`.`idventas` AS `idventas`,`movimientocaja`.`idcompras` AS `idcompras` from ((((`movimientocaja` left join `cajas` on((`movimientocaja`.`idcaja` = `cajas`.`idcaja`))) left join `rubros` on((`movimientocaja`.`nrorubro` = `rubros`.`nrorubro`))) left join `ventas` on((`movimientocaja`.`idventas` = `ventas`.`idventa`))) left join `monedas` on((`movimientocaja`.`idmoneda` = `monedas`.`idmoneda`)));

CREATE OR REPLACE VIEW `vw_numero_fac` AS (select (max(`tblfacturas`.`nro_factura`) + 1) AS `nro_factura` from `tblfacturas`);

CREATE OR REPLACE VIEW `vw_pedidos` AS select `pedidos`.`nro_factura` AS `nro_factura`,`proveedores`.`descripcion` AS `DesProveedor`,`pedidos`.`fecha_pedido` AS `fecha_pedido`,`pedidos`.`total_exenta` AS `total_exenta`,`pedidos`.`total_iva5` AS `total_iva5`,`pedidos`.`total_iva10` AS `total_iva10`,`depositos`.`descripcion` AS `DesDeposito`,`pedidos`.`cidusuario` AS `cidusuario`,`pedidos`.`forma_pago` AS `forma_pago`,`pedidos`.`subtotal` AS `subtotal`,`pedidos`.`descuento` AS `descuento`,`pedidos`.`total_pedido` AS `total_pedido`,`pedidos`.`liquidacion_iva5` AS `liquidacion_iva5`,`pedidos`.`liquidacion_iva10` AS `liquidacion_iva10`,`pedidos`.`total_impuesto` AS `total_impuesto`,`pedidos`.`iddeposito` AS `iddeposito`,`pedidos`.`idcaja` AS `idcaja`,`pedidos`.`idproveedor` AS `idproveedor`,`pedidos`.`timbrado` AS `timbrado`,`pedidos`.`nro_pedido` AS `nro_pedido`,`pedidos`.`concepto` AS `concepto`,`pedidos`.`estado` AS `estado`,`pedidos`.`idpedido` AS `idpedido`,`proveedores`.`direccion` AS `direccion`,`proveedores`.`telefono` AS `telefono`,`pedidos`.`observacion` AS `observacion`,`proveedores`.`ruc` AS `ruc` from ((`pedidos` left join `proveedores` on((`pedidos`.`idproveedor` = `proveedores`.`idproveedor`))) left join `depositos` on((`pedidos`.`iddeposito` = `depositos`.`iddeposito`)));

CREATE OR REPLACE VIEW `vw_planillainforconf` AS select `clientes`.`ruc` AS `ruc`,`clientes`.`nombres` AS `nombres`,`creditocliente`.`fecha` AS `fecha`,`creditocliente`.`vencimiento` AS `vencimiento`,`creditocliente`.`cuota` AS `cuota`,`creditocliente`.`total` AS `total`,`creditocliente`.`entrega` AS `entrega`,sum(`creditocliente`.`saldo`) AS `saldo`,`clientes`.`idcliente` AS `idcliente`,`creditocliente`.`idcredito` AS `idcredito` from (`creditocliente` left join `clientes` on((`creditocliente`.`idcliente` = `clientes`.`idcliente`))) where ((`clientes`.`inforconf` = 1) and (`creditocliente`.`saldo` > 0)) group by `clientes`.`idcliente` order by `creditocliente`.`vencimiento` desc;

CREATE OR REPLACE VIEW `vw_presupuestos` AS select `clientes`.`nombres` AS `DesCliente`,`presupuestos`.`idpresupuesto` AS `idpresupuesto`,`presupuestos`.`nro_presupuesto` AS `nro_presupuesto`,`presupuestos`.`nro_factura` AS `nro_factura`,`presupuestos`.`fecha_presupuesto` AS `fecha_presupuesto`,`presupuestos`.`forma_pago` AS `forma_pago`,`presupuestos`.`iddeposito` AS `iddeposito`,`presupuestos`.`idcaja` AS `idcaja`,`presupuestos`.`idcliente` AS `idcliente`,`presupuestos`.`idvendedor` AS `idvendedor`,`presupuestos`.`timbrado` AS `timbrado`,`presupuestos`.`total_exenta` AS `total_exenta`,`presupuestos`.`total_iva5` AS `total_iva5`,`presupuestos`.`total_iva10` AS `total_iva10`,`presupuestos`.`subtotal` AS `subtotal`,`presupuestos`.`descuento` AS `descuento`,`presupuestos`.`total_presupuesto` AS `total_presupuesto`,`presupuestos`.`liquidacion_iva5` AS `liquidacion_iva5`,`presupuestos`.`liquidacion_iva10` AS `liquidacion_iva10`,`presupuestos`.`total_impuesto` AS `total_impuesto`,`presupuestos`.`cidusuario` AS `cidusuario`,`presupuestos`.`concepto` AS `concepto`,`presupuestos`.`estado` AS `estado`,`depositos`.`descripcion` AS `DesDeposito`,`presupuestos`.`observacion` AS `observacion`,`clientes`.`direccion` AS `direccion`,`clientes`.`telefono` AS `telefono`,`clientes`.`ruc` AS `ruc`,`vendedores`.`nombres` AS `DesVendedor` from (((`presupuestos` left join `clientes` on((`presupuestos`.`idcliente` = `clientes`.`idcliente`))) left join `depositos` on((`presupuestos`.`iddeposito` = `depositos`.`iddeposito`))) left join `vendedores` on((`presupuestos`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_preventas` AS select `clientes`.`nombres` AS `DesCliente`,`preventas`.`idpreventa` AS `idpreventa`,`preventas`.`nro_preventa` AS `nro_preventa`,`preventas`.`nro_factura` AS `nro_factura`,`preventas`.`fecha_preventa` AS `fecha_preventa`,`preventas`.`forma_pago` AS `forma_pago`,`preventas`.`iddeposito` AS `iddeposito`,`preventas`.`idcaja` AS `idcaja`,`preventas`.`idcliente` AS `idcliente`,`preventas`.`idvendedor` AS `idvendedor`,`preventas`.`timbrado` AS `timbrado`,`preventas`.`total_exenta` AS `total_exenta`,`preventas`.`total_iva5` AS `total_iva5`,`preventas`.`total_iva10` AS `total_iva10`,`preventas`.`subtotal` AS `subtotal`,`preventas`.`descuento` AS `descuento`,`preventas`.`total_preventa` AS `total_preventa`,`preventas`.`liquidacion_iva5` AS `liquidacion_iva5`,`preventas`.`liquidacion_iva10` AS `liquidacion_iva10`,`preventas`.`total_impuesto` AS `total_impuesto`,`preventas`.`cidusuario` AS `cidusuario`,`preventas`.`concepto` AS `concepto`,`preventas`.`estado` AS `estado`,`depositos`.`descripcion` AS `DesDeposito`,`cajas`.`descripcion` AS `DesCaja`,`clientes`.`ruc` AS `ruc`,`clientes`.`telefono` AS `telefono`,`clientes`.`direccion` AS `direccion`,`vendedores`.`nombres` AS `DesVendedor`,`preventas`.`observacion` AS `observacion` from ((((`preventas` join `clientes` on((`preventas`.`idcliente` = `clientes`.`idcliente`))) join `depositos` on((`preventas`.`iddeposito` = `depositos`.`iddeposito`))) join `cajas` on((`preventas`.`idcaja` = `cajas`.`idcaja`))) join `vendedores` on((`preventas`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_productos` AS select `productos`.`idproducto` AS `idproducto`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `descripcion`,`productos`.`costo1` AS `costo1`,`productos`.`precio1` AS `precio1`,if(isnull(`grupoproductos`.`descripcion`),'**SIN GRUPO**',`grupoproductos`.`descripcion`) AS `DesGrupo`,`proveedores`.`descripcion` AS `DesProveedor`,`proveedores`.`telefono` AS `telefono`,`proveedores`.`celular` AS `celular` from ((`productos` left join `grupoproductos` on((`productos`.`idgrupoproducto` = `grupoproductos`.`idgrupoproducto`))) left join `proveedores` on((`productos`.`idproveedor` = `proveedores`.`idproveedor`)));

CREATE OR REPLACE VIEW `vw_productoscomprados` AS select `itemscompras`.`iditemcompra` AS `iditemcompra`,`itemscompras`.`idcompra` AS `idcompra`,`itemscompras`.`nro_factura` AS `nro_factura`,`itemscompras`.`idproducto` AS `idproducto`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`itemscompras`.`cantidad` AS `cantidad`,`itemscompras`.`pre_unitario` AS `pre_unitario`,`itemscompras`.`pre_exenta` AS `pre_exenta`,`itemscompras`.`pre_iva5` AS `pre_iva5`,`itemscompras`.`pre_iva10` AS `pre_iva10`,`itemscompras`.`nro_compra` AS `nro_compra`,`productos`.`costomedio` AS `costomedio`,`productos`.`fechafingarantia` AS `fechafingarantia`,`productos`.`diasgarantias` AS `diasgarantias`,`productos`.`fechainiciogarantia` AS `fechainiciogarantia`,`productos`.`controlagarantia` AS `controlagarantia`,`compras`.`fecha_compra` AS `fecha_compra`,`proveedores`.`descripcion` AS `DesProveedor`,`proveedores`.`ruc` AS `ruc`,`compras`.`estado` AS `estado` from (((`itemscompras` left join `productos` on((`itemscompras`.`idproducto` = `productos`.`idproducto`))) left join `compras` on((`compras`.`idcompra` = `itemscompras`.`idcompra`))) left join `proveedores` on((`compras`.`idproveedor` = `proveedores`.`idproveedor`)));

CREATE OR REPLACE VIEW `vw_productosvendidos` AS select `i`.`iditemventa` AS `iditemventa`,`i`.`idventa` AS `idventa`,`i`.`nro_factura` AS `nro_factura`,`i`.`idproducto` AS `idproducto`,`p`.`codigo` AS `codigo`,`p`.`descripcion` AS `DesProducto`,`i`.`cantidad` AS `cantidad`,`i`.`pre_unitario` AS `pre_unitario`,`i`.`pre_exenta` AS `pre_exenta`,`i`.`pre_iva5` AS `pre_iva5`,`i`.`pre_iva10` AS `pre_iva10`,`i`.`nro_venta` AS `nro_venta`,`p`.`costomedio` AS `costomedio`,`p`.`fechafingarantia` AS `fechafingarantia`,`p`.`diasgarantias` AS `diasgarantias`,`p`.`fechainiciogarantia` AS `fechainiciogarantia`,`p`.`controlagarantia` AS `controlagarantia`,`v`.`fecha_venta` AS `fecha_venta`,`c`.`ruc` AS `ruc`,`c`.`nombres` AS `DesCliente`,`v`.`estado` AS `estado`,`gp`.`idgrupoproducto` AS `idgrupoproducto`,`gp`.`descripcion` AS `grupoproducto`,`mp`.`idmarcaproducto` AS `idmarcaproducto`,`mp`.`descripcion` AS `marcaproducto`,`id`.`iddeposito` AS `iddeposito`,`d`.`descripcion` AS `desdeposito`,`v`.`idcliente` AS `idcliente`,`v`.`idvendedor` AS `idvendedor`,`ven`.`nombres` AS `desvendedor` from ((((((((`itemsventas` `i` left join `productos` `p` on((`i`.`idproducto` = `p`.`idproducto`))) left join `ventas` `v` on((`v`.`idventa` = `i`.`idventa`))) left join `clientes` `c` on((`v`.`idcliente` = `c`.`idcliente`))) left join `grupoproductos` `gp` on((`p`.`idgrupoproducto` = `gp`.`idgrupoproducto`))) left join `marcaproductos` `mp` on((`p`.`idmarcaproducto` = `mp`.`idmarcaproducto`))) left join `itemsdepositos` `id` on((`id`.`idproducto` = `p`.`idproducto`))) left join `depositos` `d` on((`d`.`iddeposito` = `id`.`iddeposito`))) left join `vendedores` `ven` on((`ven`.`idvendedor` = `v`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_productosxdepositos` AS select `itemsdepositos`.`iditemdeposito` AS `iditemdeposito`,`itemsdepositos`.`iddeposito` AS `iddeposito`,`depositos`.`descripcion` AS `DesDeposito`,`depositos`.`responsable` AS `responsable`,`itemsdepositos`.`idproducto` AS `idproducto`,`itemsdepositos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`itemsdepositos`.`cantidad` AS `cantidad`,`productos`.`costomedio` AS `costomedio`,`productos`.`precio1` AS `precio1` from ((`itemsdepositos` join `depositos` on((`itemsdepositos`.`iddeposito` = `depositos`.`iddeposito`))) join `productos` on((`itemsdepositos`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_produtos_cantidad_busqueda` AS select `productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `descripcion`,`productos`.`precio1` AS `precio1`,`productos`.`precio2` AS `precio2`,`productos`.`precio3` AS `precio3`,if(isnull(`itemsdepositos`.`cantidad`),0,`itemsdepositos`.`cantidad`) AS `cantidad`,`productos`.`idproducto` AS `idproducto`,if(isnull(`itemsdepositos`.`iddeposito`),1,`itemsdepositos`.`iddeposito`) AS `iddeposito` from (`productos` left join `itemsdepositos` on((`productos`.`idproducto` = `itemsdepositos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_recibosnombres` AS select `recibos`.`idrecibos` AS `idrecibos`,`recibos`.`letra` AS `letra`,`recibos`.`recibo` AS `recibo`,`recibos`.`fecha` AS `fecha`,`recibos`.`idcliente` AS `idcliente`,`recibos`.`importe` AS `importe`,`recibos`.`concepto` AS `concepto`,`recibos`.`montoenletras` AS `montoenletras`,`recibos`.`idcaja` AS `idcaja`,`recibos`.`estado` AS `estado`,`clientes`.`nombres` AS `DesNombre`,`clientes`.`ruc` AS `ruc`,`clientes`.`direccion` AS `direccion`,`clientes`.`telefono` AS `telefono`,`clientes`.`celular` AS `celular`,`clientes`.`fax` AS `fax`,`clientes`.`email` AS `email`,`cajas`.`descripcion` AS `DesCaja` from ((`recibos` left join `clientes` on((`recibos`.`idcliente` = `clientes`.`idcliente`))) left join `cajas` on((`recibos`.`idcaja` = `cajas`.`idcaja`)));

CREATE OR REPLACE VIEW `vw_recibosnombresproveedor` AS select sql_no_cache `recibos`.`idrecibos` AS `idrecibos`,`recibos`.`letra` AS `letra`,`recibos`.`recibo` AS `recibo`,`recibos`.`fecha` AS `fecha`,`recibos`.`importe` AS `importe`,`recibos`.`concepto` AS `concepto`,`recibos`.`montoenletras` AS `montoenletras`,`recibos`.`idcaja` AS `idcaja`,`recibos`.`estado` AS `estado`,`cajas`.`descripcion` AS `DesCaja`,`recibos`.`idproveedor` AS `idproveedor`,`proveedores`.`ruc` AS `ruc`,`proveedores`.`descripcion` AS `DesNombre`,`proveedores`.`fax` AS `fax`,`proveedores`.`celular` AS `celular`,`proveedores`.`telefono` AS `telefono`,`proveedores`.`direccion` AS `direccion`,`proveedores`.`email` AS `email` from ((`recibos` left join `cajas` on((`recibos`.`idcaja` = `cajas`.`idcaja`))) left join `proveedores` on((`recibos`.`idproveedor` = `proveedores`.`idproveedor`)));

CREATE OR REPLACE VIEW `vw_recibosupervisores` AS select `recibos`.`idrecibos` AS `idrecibos`,`recibos`.`letra` AS `letra`,`recibos`.`recibo` AS `recibo`,`recibos`.`fecha` AS `fecha`,`recibos`.`idcliente` AS `idcliente`,`recibos`.`importe` AS `importe`,`recibos`.`concepto` AS `concepto`,`recibos`.`montoenletras` AS `montoenletras`,`recibos`.`idcaja` AS `idcaja`,`recibos`.`estado` AS `estado`,`recibos`.`idproveedor` AS `idproveedor`,`recibos`.`idvendedor` AS `idvendedor`,`recibos`.`idsupervisor` AS `idsupervisor`,`supervisores`.`nombres` AS `DesNombre`,`supervisores`.`documento` AS `documento`,`cajas`.`descripcion` AS `DesCaja` from ((`recibos` left join `supervisores` on((`recibos`.`idsupervisor` = `supervisores`.`idsupervisor`))) left join `cajas` on((`recibos`.`idcaja` = `cajas`.`idcaja`)));

CREATE OR REPLACE VIEW `vw_recibovendedores` AS select `recibos`.`idrecibos` AS `idrecibos`,`recibos`.`letra` AS `letra`,`recibos`.`recibo` AS `recibo`,`recibos`.`fecha` AS `fecha`,`recibos`.`idcliente` AS `idcliente`,`recibos`.`importe` AS `importe`,`recibos`.`concepto` AS `concepto`,`recibos`.`montoenletras` AS `montoenletras`,`recibos`.`idcaja` AS `idcaja`,`recibos`.`estado` AS `estado`,`recibos`.`idproveedor` AS `idproveedor`,`recibos`.`idvendedor` AS `idvendedor`,`recibos`.`idsupervisor` AS `idsupervisor`,`vendedores`.`nombres` AS `DesNombre`,`vendedores`.`documento` AS `documento`,`cajas`.`descripcion` AS `DesCaja` from ((`recibos` left join `vendedores` on((`recibos`.`idvendedor` = `vendedores`.`idvendedor`))) left join `cajas` on((`recibos`.`idcaja` = `cajas`.`idcaja`)));

CREATE OR REPLACE VIEW `vw_solicitudcredito` AS select `solicitudcredito`.`idsolicitud` AS `idsolicitud`,`solicitudcredito`.`idvendedor` AS `idvendedor`,`clientes`.`ruc` AS `ruc`,`clientes`.`nombres` AS `DesCliente`,`clientes`.`telefono` AS `telefono`,`clientes`.`celular` AS `celular`,`clientes`.`fax` AS `fax`,`solicitudcredito`.`monto` AS `monto`,`solicitudcredito`.`plazo` AS `plazo`,`solicitudcredito`.`cuota` AS `cuota`,`solicitudcredito`.`fechaingreso` AS `fechaingreso`,(case when (`solicitudcredito`.`estado` = 1) then _utf8'Pendiente' when (`solicitudcredito`.`estado` = 2) then _utf8'Aprobado' else _utf8'Rechazado' end) AS `estado`,`solicitudcredito`.`estado` AS `nro_estado`,`solicitudcredito`.`fecha_aprobacion` AS `fecha_aprobacion`,`listasbancos`.`descripcion` AS `DesBancoLista`,`vendedores`.`nombres` AS `DesVendedor`,`vendedores`.`telefonos` AS `telefonos` from (((`solicitudcredito` left join `clientes` on((`solicitudcredito`.`idcliente` = `clientes`.`idcliente`))) left join `listasbancos` on((`solicitudcredito`.`idbancolista` = `listasbancos`.`idlistabanco`))) left join `vendedores` on((`solicitudcredito`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_timbradolocal` AS select `sucursal`.`nombre` AS `nombre`,`cajas`.`descripcion` AS `descripcion`,`timbradolocal`.`timbrado` AS `timbrado`,`timbradolocal`.`factura` AS `factura`,`timbradolocal`.`vencimiento` AS `vencimiento`,`sucursal`.`idsucursal` AS `idsucursal` from ((`timbradolocal` left join `sucursal` on((`timbradolocal`.`idsucursal` = `sucursal`.`idsucursal`))) left join `cajas` on((`timbradolocal`.`idcaja` = `cajas`.`idcaja`)));

CREATE OR REPLACE VIEW `vw_trasladodepositos` AS select `trasladodepositos`.`idtrasladodeposito` AS `idtrasladodeposito`,`depositos`.`descripcion` AS `DesDepositoOrigen`,`vw_depositos`.`descripcion` AS `DesDepositoDestino`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `DesProducto`,`trasladodepositos`.`cantidad` AS `cantidad`,`trasladodepositos`.`fecha` AS `fecha`,`trasladodepositos`.`hora` AS `hora` from (((`trasladodepositos` left join `depositos` on((`trasladodepositos`.`iddepositoorigen` = `depositos`.`iddeposito`))) left join `productos` on((`trasladodepositos`.`idproducto` = `productos`.`idproducto`))) left join `vw_depositos` on((`trasladodepositos`.`iddepositodestino` = `vw_depositos`.`iddeposito`)));

CREATE OR REPLACE VIEW `vw_vencimiento_productos` AS select `productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `descripcion`,`lote_producto`.`lote` AS `lote`,`lote_producto`.`vencimiento` AS `vencimiento`,`lote_producto`.`cantidad` AS `cantidad` from (`lote_producto` left join `productos` on((`lote_producto`.`idproducto` = `productos`.`idproducto`)));

CREATE OR REPLACE VIEW `vw_vencimientosclientes` AS select `creditocliente`.`idcredito` AS `idcredito`,`creditocliente`.`idventa` AS `idventa`,`creditocliente`.`nro_venta` AS `nro_venta`,`creditocliente`.`nro_factura` AS `nro_factura`,`creditocliente`.`total` AS `total`,`creditocliente`.`entrega` AS `entrega`,`creditocliente`.`saldo` AS `saldo`,`creditocliente`.`vencimiento` AS `vencimiento`,`creditocliente`.`cuota` AS `cuota`,`creditocliente`.`fecha` AS `fecha`,`creditocliente`.`dia` AS `dia`,`creditocliente`.`idcliente` AS `idcliente`,`clientes`.`nombres` AS `DesCliente`,`clientes`.`contacto` AS `contacto`,`clientes`.`telefono_contacto` AS `telefono_contacto`,`clientes`.`ruc` AS `ruc`,`clientes`.`direccion` AS `direccion`,`clientes`.`telefono` AS `telefono`,`clientes`.`celular` AS `celular`,`clientes`.`fax` AS `fax`,`clientes`.`email` AS `email`,`clientes`.`idvendedor` AS `idvendedor`,`vendedores`.`nombres` AS `DesVendedor`,(`creditocliente`.`vencimiento` - curdate()) AS `atraso`,`clientes`.`refinanciacion` AS `refinanciacion`,`clientes`.`judicial1` AS `judicial1`,`clientes`.`inforconf` AS `inforconf`,`clientes`.`estado` AS `estado` from ((`creditocliente` left join `clientes` on((`creditocliente`.`idcliente` = `clientes`.`idcliente`))) left join `vendedores` on((`clientes`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_vendedorsupervisor` AS select `vendedores`.`idvendedor` AS `idvendedor`,`vendedores`.`idsupervisor` AS `idsupervisor`,`supervisores`.`nombres` AS `DesSupervisor`,`vendedores`.`estado` AS `estado` from (`vendedores` left join `supervisores` on((`vendedores`.`idsupervisor` = `supervisores`.`idsupervisor`)));

CREATE OR REPLACE VIEW `vw_ventas` AS select `clientes`.`nombres` AS `DesCliente`,`ventas`.`idventa` AS `idventa`,`ventas`.`nro_venta` AS `nro_venta`,`ventas`.`nro_factura` AS `nro_factura`,`ventas`.`fecha_venta` AS `fecha_venta`,`ventas`.`forma_pago` AS `forma_pago`,`ventas`.`iddeposito` AS `iddeposito`,`ventas`.`idcaja` AS `idcaja`,`ventas`.`idcliente` AS `idcliente`,`ventas`.`idvendedor` AS `idvendedor`,`ventas`.`timbrado` AS `timbrado`,`ventas`.`total_exenta` AS `total_exenta`,`ventas`.`total_iva5` AS `total_iva5`,`ventas`.`total_iva10` AS `total_iva10`,`ventas`.`subtotal` AS `subtotal`,`ventas`.`descuento` AS `descuento`,`ventas`.`total_venta` AS `total_venta`,`ventas`.`liquidacion_iva5` AS `liquidacion_iva5`,`ventas`.`liquidacion_iva10` AS `liquidacion_iva10`,`ventas`.`total_impuesto` AS `total_impuesto`,`ventas`.`cidusuario` AS `cidusuario`,`ventas`.`concepto` AS `concepto`,`ventas`.`estado` AS `estado`,`depositos`.`descripcion` AS `DesDeposito`,`cajas`.`descripcion` AS `DesCaja`,`clientes`.`ruc` AS `ruc`,`clientes`.`telefono` AS `telefono`,`clientes`.`direccion` AS `direccion`,`vendedores`.`nombres` AS `DesVendedor`,`ventas`.`observacion` AS `observacion`,`ventas`.`vencimiento` AS `vencimiento`,`ventas`.`moneda_real` AS `moneda_real`,`ventas`.`moneda_guarani` AS `moneda_guarani`,`ventas`.`moneda_euro` AS `moneda_euro`,`ventas`.`moneda_peso` AS `moneda_peso`,`ventas`.`moneda_dolar` AS `moneda_dolar`,`ventas`.`cot_guarani` AS `cot_guarani`,`ventas`.`cot_euro` AS `cot_euro`,`ventas`.`cot_peso` AS `cot_peso`,`ventas`.`cot_real` AS `cot_real`,`ventas`.`cot_dolar` AS `cot_dolar`,`ventas`.`entrega_real` AS `entrega_real`,`ventas`.`entrega_guarani` AS `entrega_guarani`,`ventas`.`entrega_euro` AS `entrega_euro`,`ventas`.`entrega_peso` AS `entrega_peso`,`ventas`.`entrega_dolar` AS `entrega_dolar`,`ventas`.`vuelto_real` AS `vuelto_real`,`ventas`.`vuelto_guarani` AS `vuelto_guarani`,`ventas`.`vuelto_peso` AS `vuelto_peso`,`ventas`.`vuelto_dolar` AS `vuelto_dolar`,`ventas`.`tipoboleta` AS `tipoboleta`,`ventas`.`nro_boleta` AS `nro_boleta`,`ventas`.`tipo_venta` AS `tipo_venta`,if((`ventas`.`forma_pago` = 1),_utf8'Contado',_utf8'Crédito') AS `forma_pago2`,`clientes`.`celular` AS `celular`,`clientes`.`dv` AS `dv`,`ventas`.`mesa` AS `mesa`,`ventas`.`barra` AS `barra`,`ventas`.`cierre` AS `cierre`,`ventas`.`idmoneda` AS `idmoneda`,`ventas`.`cuota` AS `cuota`,`ventas`.`facturaporparte` AS `facturaporparte`,`ventas`.`hora_venta` AS `hora_venta`,`ventas`.`idsucursal` AS `idsucursal` from ((((`ventas` left join `clientes` on((`ventas`.`idcliente` = `clientes`.`idcliente`))) left join `depositos` on((`ventas`.`iddeposito` = `depositos`.`iddeposito`))) left join `cajas` on((`ventas`.`idcaja` = `cajas`.`idcaja`))) left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_ventas_por_proveedor` AS select `productos`.`idproducto` AS `idproducto`,`productos`.`codigo` AS `codigo`,`productos`.`descripcion` AS `Desproducto`,`itemsventas`.`pre_unitario` AS `pre_unitario`,`itemsventas`.`cantidad` AS `cantidad`,(`itemsventas`.`pre_unitario` * `itemsventas`.`cantidad`) AS `Total`,`ventas`.`fecha_venta` AS `fecha_venta`,`ventas`.`idvendedor` AS `idvendedor`,`vendedores`.`nombres` AS `DesVendedor`,`ventas`.`estado` AS `estado`,`productos`.`idproveedor` AS `idproveedor`,`proveedores`.`descripcion` AS `DesProveedor` from ((((`ventas` left join `itemsventas` on((`ventas`.`idventa` = `itemsventas`.`idventa`))) left join `productos` on((`itemsventas`.`idproducto` = `productos`.`idproducto`))) left join `proveedores` on((`productos`.`idproveedor` = `proveedores`.`idproveedor`))) left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`)));

CREATE OR REPLACE VIEW `vw_ventasporvendedor` AS select `ventas`.`fecha_venta` AS `fecha_venta`,sum(`ventas`.`total_venta`) AS `total_venta`,`vendedores`.`idvendedor` AS `idvendedor`,`vendedores`.`nombres` AS `DesVendedor`,`supervisores`.`nombres` AS `DesSupervisor`,`ventas`.`estado` AS `estado` from ((`ventas` left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) left join `supervisores` on((`vendedores`.`idsupervisor` = `supervisores`.`idsupervisor`))) where (`vendedores`.`estado` = 1) group by `vendedores`.`idvendedor`;

CREATE OR REPLACE VIEW `vw_ventasporvendedor2` AS select `ventas`.`nro_venta` AS `nro_venta`,`ventas`.`fecha_venta` AS `fecha_venta`,`clientes`.`nombres` AS `DesCliente`,`clientes`.`ruc` AS `ruc`,`ventas`.`total_exenta` AS `total_exenta`,`ventas`.`total_iva5` AS `total_iva5`,`ventas`.`total_iva10` AS `total_iva10`,`ventas`.`total_impuesto` AS `total_impuesto`,`ventas`.`total_venta` AS `total_venta`,`vendedores`.`nombres` AS `DesVendedor`,`ventas`.`idvendedor` AS `idvendedor`,`ventas`.`idcliente` AS `idcliente`,`ventas`.`idventa` AS `idventa`,`ventas`.`estado` AS `estado` from ((`ventas` left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) left join `clientes` on((`ventas`.`idcliente` = `clientes`.`idcliente`)));

CREATE OR REPLACE VIEW `vw_ventasporvendedoryfecha` AS select `ventas`.`fecha_venta` AS `fecha_venta`,sum(`ventas`.`total_venta`) AS `SUM(total_venta)`,`vendedores`.`idvendedor` AS `idvendedor`,`vendedores`.`nombres` AS `DesVendedor`,`ventas`.`estado` AS `estado` from (`ventas` left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) where (`vendedores`.`estado` = 1) group by `ventas`.`fecha_venta`,`vendedores`.`idvendedor` order by `vendedores`.`idvendedor`;

CREATE OR REPLACE VIEW `vw_ventassupervisor` AS select `ventas`.`fecha_venta` AS `fecha_venta`,sum(`ventas`.`total_venta`) AS `total_venta`,`vendedores`.`nombres` AS `DesVendedor`,`supervisores`.`nombres` AS `DesSupervisor`,`ventas`.`estado` AS `estado` from ((`ventas` left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) left join `supervisores` on((`vendedores`.`idsupervisor` = `supervisores`.`idsupervisor`))) where (`vendedores`.`nombres` = `supervisores`.`nombres`) group by `vendedores`.`idsupervisor`;

CREATE OR REPLACE VIEW `vw_ventassupervisor2` AS select `ventas`.`nro_venta` AS `nro_venta`,`ventas`.`fecha_venta` AS `fecha_venta`,`clientes`.`nombres` AS `DesCliente`,`clientes`.`ruc` AS `ruc`,`ventas`.`total_exenta` AS `total_exenta`,`ventas`.`total_iva5` AS `total_iva5`,`ventas`.`total_iva10` AS `total_iva10`,`ventas`.`total_impuesto` AS `total_impuesto`,`ventas`.`total_venta` AS `total_venta`,`vendedores`.`nombres` AS `DesVendedor`,`ventas`.`idvendedor` AS `idvendedor`,`ventas`.`idcliente` AS `idcliente`,`ventas`.`idventa` AS `idventa`,`supervisores`.`nombres` AS `DesSupervisor`,`vendedores`.`idsupervisor` AS `idsupervisor`,`ventas`.`estado` AS `estado` from (((`ventas` left join `vendedores` on((`ventas`.`idvendedor` = `vendedores`.`idvendedor`))) left join `clientes` on((`ventas`.`idcliente` = `clientes`.`idcliente`))) left join `supervisores` on((`vendedores`.`idsupervisor` = `supervisores`.`idsupervisor`)));

-- =============================================================================
-- Clean Factory Seed Data (Ready to run on first startup)
-- =============================================================================
INSERT INTO `datosempresa` (`iddatosempresa`, `descripcion`, `ruc`, `direccion`, `telefonos`, `utilidad1`, `utilidad2`, `utilidad3`, `utilidad4`, `utilidad5`, `utilidad6`, `utilidad7`, `interescuota`, `interespunitorio`, `reclamos`, `honorariojudicial`, `cobrocaja`, `idcliente`, `idvendedor`, `cantvias`, `veterinaria`, `comercial`, `restaurant`, `habilitacobro`)
VALUES (1, 'SEFIROS POS', '80000000-0', 'Casa Central', '0000-000-000', 35, 45, 60, 80, 100, 120, 0, 3, 6, 25000, 10, 1, 132, 10, 1, 0, 1, 0, 1);

INSERT INTO `sucursal` (`idsucursal`, `nombre`, `dir`, `tel`) VALUES (1, 'CASA CENTRAL', 'Casa Central', '');
INSERT INTO `depositos` (`iddeposito`, `descripcion`, `idsucursal`) VALUES (1, 'DEPOSITO PRINCIPAL', 1);
INSERT INTO `cajas` (`idcaja`, `descripcion`, `saldo`, `idsucursal`, `dessucursal`) VALUES (1, 'CAJA 1', 0, 1, 'CASA CENTRAL'), (2, 'CAJA 2', 0, 1, 'CASA CENTRAL'), (3, 'CAJA 3', 0, 1, 'CASA CENTRAL'), (4, 'CAJA 4', 0, 1, 'CASA CENTRAL'), (5, 'CAJA 5', 0, 1, 'CASA CENTRAL');
INSERT INTO `grupos` (`nlineno`, `cidgrupo`, `cdescribe`, `caccesos`, `avisos`, `tool`, `cidusuario`, `btncompra`, `btnventa`, `btnpedido`, `btnpresu`, `btnvenci`, `btncliente`, `btnproveedor`, `btnvendedor`, `btnproducto`, `btnpago`, `btncobro`, `btnmovcaja`) VALUES (1, 'ADMIN', 'ADMINISTRADOR', '', 1, 1, 'ADMINISTRA', 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
INSERT INTO `usuarios` (`nlineno`, `cidgrupo`, `cidusuario`, `cdescribe`, `cpassword`, `csuspendido`, `idsucursal`, `tipovendedor`, `idcaja`, `idvendedor`, `iddeposito`) VALUES (1, 'ADMIN', 'ADMINISTRA', 'ADMINISTRADOR DEL SISTEMA', '12020', 0, 1, 1, 1, 10, 1);
INSERT INTO `clientes` (`idcliente`, `ruc`, `nombres`, `direccion`, `telefonos`, `estado`, `lineacredito`) VALUES (132, 'X', 'CLIENTE OCASIONAL', '', '', 1, 0);
INSERT INTO `vendedores` (`idvendedor`, `nombres`, `estado`) VALUES (10, 'VENDEDOR MOSTRADOR', 1);
INSERT INTO `timbradolocal` (`idtimbradolocal`, `idsucursal`, `idcaja`, `timbrado`, `n1`, `n2`, `n3`, `factura`, `cantidad`, `fechainicio`, `vencimiento`) VALUES (1, 1, 1, '12345678', 1, 1, 1, '001-001-0000001', 9999999, CURDATE(), DATE_ADD(CURDATE(), INTERVAL 1 YEAR));

INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (1, 'PYG - Guaraní Paraguayo (Paraguay)', 1, 1, 0, 1, '*', 1);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (2, 'ARS - Peso Argentino (Argentina)', 7.6, 7.68, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (3, 'BOB - Boliviano (Bolivia)', 1121.56, 1132.78, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (4, 'BRL - Real Brasileño (Brasil)', 1336.21, 1349.57, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (5, 'CLP - Peso Chileno (Chile)', 8.03, 8.11, 0, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (6, 'COP - Peso Colombiano (Colombia)', 1.78, 1.8, 0, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (7, 'CRC - Colón Costarricense (Costa Rica)', 15.14, 15.29, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (8, 'CUP - Peso Cubano (Cuba)', 322.92, 326.15, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (9, 'DOP - Peso Dominicano (Rep. Dominicana)', 128.74, 130.03, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (10, 'USD - Dólar Estadounidense (Ecuador / El Salvador / Panamá / Regional)', 7750, 7827.5, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (11, 'GTQ - Quetzal Guatemalteco (Guatemala)', 1003.89, 1013.93, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (12, 'HNL - Lempira Hondureño (Honduras)', 307.54, 310.62, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (13, 'HTG - Gourde Haitiano (Haití)', 59.16, 59.75, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (14, 'MXN - Peso Mexicano (México)', 381.77, 385.59, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (15, 'NIO - Córdoba Nicaragüense (Nicaragua)', 210.6, 212.71, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (16, 'PAB - Balboa Panameño (Panamá)', 7750, 7827.5, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (17, 'PEN - Sol Peruano (Perú)', 2061.17, 2081.78, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (18, 'UYU - Peso Uruguayo (Uruguay)', 182.35, 184.17, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (19, 'VES - Bolívar Venezolano (Venezuela)', 166.67, 168.34, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (20, 'BZD - Dólar Beliceño (Belice)', 3875, 3913.75, 2, 0, '*', 0);
INSERT INTO `monedas` (`idmoneda`, `descripcion`, `compra`, `venta`, `decimales`, `tronco`, `operacion`, `tipo`) VALUES (21, 'EUR - Euro (Referencia Internacional)', 8244.68, 8327.13, 2, 0, '*', 0);

SET FOREIGN_KEY_CHECKS = 1;

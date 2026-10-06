import {Assert} from "@SignalRGB/Errors.js";
import permissions from "@SignalRGB/permissions";
export function Name() { return "MSI Monitor Controller"; }
export function VendorId() { return 0x1462; }
export function ProductId() { return 0x3FA4; }
export function Publisher() { return "Derek Huber & WhirlwindFX"; }
export function Size() { return [1, 1]; }
export function Type() { return "Hid"; }
export function DefaultPosition(){ return [0, 0]; }
export function DefaultScale(){ return 1.0; }
export function DeviceType(){ return "other"; }
export function Validate(endpoint) { return endpoint.interface === 0 || endpoint.interface === -1; }
export function ImageUrl(){ return "https://assets.signalrgb.com/devices/brands/msi/monitors/generic-monitor.png"; }
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas" },
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde" }
	];
}

export function LedNames() {
	return MSIMonitor.getLedNames();
}

export function LedPositions() {
	return MSIMonitor.getLedPositions();
}

/** @param {UpdatedPermissions} updatedPermissions */
function onPermissionsUpdated(updatedPermissions){
	console.log(updatedPermissions);

	if(updatedPermissions["lighting"] === true){
		MSIMonitor.findMonitorModel();
	}
}

export function Initialize() {
	permissionManager.Register();
	
	if(permissionManager.GetPermission("lighting")) {
		MSIMonitor.findMonitorModel();
	}
}

export function Render() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	MSIMonitor.sendColors();
}

export function Shutdown(SystemSuspending) {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;
	MSIMonitor.sendColors(color);
}

class MSIMonitorProtocol {
	constructor() {

		this.config = {
			ledNames :[],
			ledPositions : [],
			ledIndexes : [],
			size : [0, 0],
			rgbPacketIndexOffset: 0,
			rgbPacketLength: 0,
			startingByte: 0x00,
			protocol: "MAG",
			model: 0,
			endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
		};

		this.idDict = {
			35 : "MAG271CR",
			49 : "MPG27CQ",
			54 : "MAG271CQR",
			58 : "MAG321CQR",
			61 : "MAG322CQRV",
			64 : "MAG274QRF",
			69 : "MAG272CQR",
			74 : "MAG274R2",
			80 : "MAG272CR",
			82 : "MAG301CR2",
			86 : "MAG240CR",
			101 : "MAG274QRFQD",
			110 : "MAG301RF",
			113 : "MPG341QR",
			114 : "MAG273R2",
			119 : "MPG321URQD",
			125 : "MAG245R2",
			128 : "MAG281URF",
			139 : "MAG401QR",
			143 : "MPG275CQRXF",
			144 : "MEG324C",
			154 : "MAG274QRFQDE2",
			158 : "MPG271QRX",
			162 : "MPG275CQRXF", // eslint-disable-line no-dupe-keys
			165 : "MPG341CQPX", // NO LEDS
			167:  "MAG275CQRFQDE2",
			173 : "MPG321CURX",
			177 : "MAG322URDF",
		};

		this.modelDict = {
			MAG271CR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MAG271CR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG27CQ : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MPG27CQ",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG272CQR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MAG272CQR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG272CR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MAG272CR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG301CR2 : {
				mapping: [
					0, 1, 2, 3, 4, 5,
					6, 7, 8, 9, 10, 11,
					12, 13, 14, 15, 16, 17,
					18, 19, 20, 21, 22, 23
				],
				positioning: [
					[5, 11], [4, 10], [3, 9], [2, 8], [1, 7], [0, 6],
					[0, 5], [1, 4], [2, 3], [3, 2], [4, 1], [5, 0],
					[6, 0], [7, 1], [8, 2], [9, 3], [10, 4], [11, 5],
					[11, 6], [10, 7], [9, 8], [8, 9], [7, 10], [6, 11]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16", "LED 17", "LED 18", "LED 19", "LED 20", "LED 21", "LED 22", "LED 23", "LED 24", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG301CR2",
				ledCount: 24,
				size: [12, 12],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG301RF : {
				mapping: [
					0, 1, 2, 3, 4, 5,
					6, 7, 8, 9, 10, 11,
					12, 13, 14, 15, 16, 17,
					18, 19, 20, 21, 22, 23
				],
				positioning: [
					[5, 11], [4, 10], [3, 9], [2, 8], [1, 7], [0, 6],
					[0, 5], [1, 4], [2, 3], [3, 2], [4, 1], [5, 0],
					[6, 0], [7, 1], [8, 2], [9, 3], [10, 4], [11, 5],
					[11, 6], [10, 7], [9, 8], [8, 9], [7, 10], [6, 11]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16", "LED 17", "LED 18", "LED 19", "LED 20", "LED 21", "LED 22", "LED 23", "LED 24", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG301RF",
				ledCount: 24,
				size: [12, 12],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG322CQRV : {
				mapping: [
					0, 1, 2, 3, 4, 5,
					6, 7, 8, 9, 10, 11,
					12, 13, 14, 15, 16, 17,
					18, 19, 20, 21, 22, 23
				],
				positioning: [
					[5, 11], [4, 10], [3, 9], [2, 8], [1, 7], [0, 6],
					[0, 5], [1, 4], [2, 3], [3, 2], [4, 1], [5, 0],
					[6, 0], [7, 1], [8, 2], [9, 3], [10, 4], [11, 5],
					[11, 6], [10, 7], [9, 8], [8, 9], [7, 10], [6, 11]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16", "LED 17", "LED 18", "LED 19", "LED 20", "LED 21", "LED 22", "LED 23", "LED 24", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG322CQRV",
				ledCount: 24,
				size: [12, 12],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG271CQR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MAG271CQR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG321CQR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MAG321CQR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG274QRF : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG274QRF",
				ledCount: 12,
				size: [12, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG274QRFQD : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG274QRF-QD",
				ledCount: 12,
				size: [12, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG341QR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MPG341QR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG273R2 : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG273R2",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG274R2 : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
				],
				positioning: [
					[11, 0], [10, 0], [9, 0], [8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG274R2",
				ledCount: 12,
				size: [12, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG281URF : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG281URF ",
				ledCount: 12,
				size: [12, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MEG324C : {
				mapping: [
					0, 1, 2, 3,	4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0], [12, 0], [13, 0], [14, 0], [15, 0], [16, 0], [17, 0], [18, 0], [19, 0], [20, 0], [21, 0], [22, 0], [23, 0], [24, 0], [25, 0], [26, 0], [27, 0], [28, 0], [29, 0], [30, 0], [31, 0], [32, 0], [33, 0], [34, 0], [35, 0], [36, 0], [37, 0], [38, 0], [39, 0],
				],
				names: [
					"LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16", "LED 17", "LED 18", "LED 19", "LED 20", "LED 21", "LED 22", "LED 23", "LED 24", "LED 25", "LED 26", "LED 27", "LED 28", "LED 29", "LED 30", "LED 31", "LED 32", "LED 33", "LED 34", "LED 35", "LED 36", "LED 37", "LED 38", "LED 39", "LED 40",
				],
				USBPacketSize: 725,
				displayName: "MSI MEG342C",
				ledCount: 40,
				size: [40, 1],
				protocol: "MEG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG245R2 : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG245R2",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG274QRFQDE2 : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG274QRF-QD E2",
				ledCount: 12,
				size: [12, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG240CR : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8
				],
				positioning: [
					[8, 0], [7, 0], [6, 0], [5, 0], [4, 0], [3, 0], [2, 0], [1, 0], [0, 0]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", ],
				startingByte: 0x71,
				firstRGBDataUSBPacketIndex: 50,
				USBPacketSize: 78,
				displayName: "MSI MAG240CR",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG341CQPX : {
				mapping: [],
				positioning: [],
				names: [],
				startingByte: 0,
				firstRGBDataUSBPacketIndex: 0,
				USBPacketSize: 0,
				displayName: "MSI MPG 341CQPX",
				ledCount: 0,
				size: [1, 1],
				protocol: "",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG321CURX : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7,
				],
				positioning: [
					[0, 0], [1, 0], [3, 0], [4, 0], [2, 1], [2, 2], [2, 3], [2, 4]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MPG 321CURX",
				ledCount: 9,
				size: [5, 5],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG275CQRXF : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7,
				],
				positioning: [
					[0, 0], [1, 0], [3, 0], [4, 0], [2, 1], [2, 2], [2, 3], [2, 4]
				],
				names: [ "LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MPG 275CQRXF",
				ledCount: 9,
				size: [5, 5],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG321URQD : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17,
					18, 19, 20, 21, 22, 23
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0],
					[0, 1], [1, 1], [2, 1], [3, 1], [4, 1], [5, 1], [6, 1], [7, 1],
					[0, 2], [1, 2], [2, 2], [3, 2], [4, 2], [5, 2], [6, 2], [7, 2]
				],
				names: [
					"LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8",
					"LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16",
					"LED 17", "LED 18", "LED 19", "LED 20", "LED 21", "LED 22", "LED 23", "LED 24"
				 ],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MPG 321URQD",
				ledCount: 24,
				size: [8, 3],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG401QR: {
				displayName: "MSI MAG401QR",
				mapping: [
					0, 1, 2, 3,	4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0], [12, 0], [13, 0], [14, 0], [15, 0], [16, 0], [17, 0], [18, 0], [19, 0]
				],
				names: [
					"LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16", "LED 17", "LED 18", "LED 19", "LED 20"
				],
				ledCount: 20,
				size: [20, 1],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MPG271QRX : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0]
				],
				names: ["LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MPG 271QRX",
				ledCount: 10,
				size: [10, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG322URDF : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0]
				],
				names: ["LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MSI MAG322URDF",
				ledCount: 9,
				size: [9, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
			MAG275CQRFQDE2 : {
				mapping: [
					0, 1, 2, 3, 4, 5, 6, 7, 8, 9
				],
				positioning: [
					[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0]
				],
				names: ["LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10"],
				startingByte: 0x72,
				firstRGBDataUSBPacketIndex: 95,
				USBPacketSize: 168,
				displayName: "MAG 275CQRF-QD E2",
				ledCount: 10,
				size: [10, 1],
				protocol: "MAG",
				endpoint: { "interface": 0, "usage": 0x0000, "usage_page": 0x0001, "collection": 0x0000 },
			},
		};
	}

	getLedNames() { return this.config.ledNames; }
	setLedNames(ledNames) { this.config.ledNames = ledNames; }

	getLedPositions() { return this.config.ledPositions; }
	setLedPositions(ledPositions) { this.config.ledPositions = ledPositions; }

	getLedIndexes() { return this.config.ledIndexes; }
	setLedIndexes(ledIndexes) { this.config.ledIndexes = ledIndexes; }

	getSize() { return this.config.size; }
	setSize(size) { this.config.size = size; }

	getRgbPacketIndexOffset() { return this.config.rgbPacketIndexOffset; }
	setRgbPacketIndexOffset(rgbPacketIndexOffset) { this.config.rgbPacketIndexOffset = rgbPacketIndexOffset; }

	getPacketLength() { return this.config.rgbPacketLength; }
	setPacketLength(packetLength) { this.config.rgbPacketLength = packetLength; }

	getStartingByte() { return this.config.startingByte; }
	setStartingByte(startingByte) { this.config.startingByte = startingByte; }

	getProtocol() { return this.config.protocol; }
	setProtocol(protocol) { this.config.protocol = protocol; }

	getModel() { return this.config.model; }
	setModel(model) { this.config.model = model; }

	getEndpoint() { return this.config.endpoint; }
	setEndpoint(endpoint) { this.config.endpoint = endpoint; }

	findMonitorModel() {
		device.clearReadBuffer();
		device.write([0x01, 0x35, 0x38, 0x30, 0x30, 0x31, 0x34, 0x30, 0x0D], 64);

		const returnPacket1 = device.read([1], 64);
		const model = returnPacket1[10];

		this.setModel(model);
		device.log("Model ID: " + model);

		this.setDeviceParameters(model);

		device.write([0x01, 0xB0], 64);

		const returnPacket = device.read([0x00], 64);
		const highByte = returnPacket[3];
		const lowByte = returnPacket[2];
		device.log("High Byte: " + highByte);
		device.log("Low Byte: " + lowByte);

	}

	setDeviceParameters(deviceID) {

		let monitor = [];

		if (this.modelDict[this.idDict[deviceID]]) {
			monitor = this.modelDict[this.idDict[deviceID]];
		}else{
			console.log(`Model ${deviceID} not found in library! Contact the devs on Discord.`);

			return;
		}

		this.setLedIndexes(monitor.mapping);
		this.setLedNames(monitor.names);
		this.setLedPositions(monitor.positioning);
		this.setSize(monitor.size);
		this.setRgbPacketIndexOffset(monitor.firstRGBDataUSBPacketIndex);
		this.setPacketLength(monitor.USBPacketSize);
		this.setStartingByte(monitor.startingByte);
		this.setProtocol(monitor.protocol);
		this.setEndpoint(monitor.endpoint);

		device.setName(`MSI ${this.idDict[deviceID]} Monitor`);
		device.setSize(this.getSize());
		device.setControllableLeds(this.getLedNames(), this.getLedPositions());
	}

	sendColors(overrideColor) {

		const model = this.getModel();

		if (this.idDict[model]) {
			switch (this.getProtocol()) {
			case "MAG":
				this.sendColorsMAG(overrideColor);
				break;
			case "MEG":
				this.sendColorsMEG(overrideColor);
				break;
			default:
				break;
			}
		}
	}

	sendColorsMAG(overrideColor) {
		const packet = [];
		const vLedPositions = this.getLedPositions();
		const vLedKeys = this.getLedIndexes();
		const rgbOffset = this.getRgbPacketIndexOffset();
		const packetLength = this.getPacketLength();

		//initial data
		packet[0] = this.getStartingByte();
		packet[1]  = 0x01;
		packet[5]  = 0x01;
		packet[6]  = 0x64;
		packet[12] = 0x01;
		packet[16] = 0x01;
		packet[17] = 0x64;

		//ff padding - 0xff 0x00 0x00 ... 0xff 0x00 0x00
		for (let ffPaddingIndex = 23; ffPaddingIndex < rgbOffset; ffPaddingIndex += 3) {
			packet[ffPaddingIndex] = 0xff;
		}

		//rgb data
		for (let ledIndex = 0; ledIndex < vLedPositions.length; ledIndex++) {
			const curLedXCoord = vLedPositions[ledIndex][0];
			const curLedYCoord = vLedPositions[ledIndex][1];
			let color;

			if (overrideColor) {
				color = hexToRgb(overrideColor);
			} else if (LightingMode === "Forced") {
				color = hexToRgb(forcedColor);
			} else {
				color = device.color(curLedXCoord, curLedYCoord);
			}

			packet[ledIndex * 3 + rgbOffset] = color[0];
			packet[ledIndex * 3 + rgbOffset + 1] = color[1];
			packet[ledIndex * 3 + rgbOffset + 2] = color[2];
		}

		//more ff padding - 0xff 0x00 0x00 ... 0xff 0x00 0x00
		for (let ffPaddingIndex = rgbOffset + vLedKeys.length * 3; ffPaddingIndex < packetLength - 1; ffPaddingIndex += 3) {
			packet[ffPaddingIndex] = 0xff;
		}

		device.send_report(packet, packetLength);
	}

	sendColorsMEG(overrideColor) {
		const RGBData = [];
		const vLedPositions = this.getLedPositions();
		const packetLength = this.getPacketLength();

		//rgb data
		for (let iIdx = 0; iIdx < vLedPositions.length; iIdx++) {
			const curLedXCoord = vLedPositions[iIdx][0];
			const curLedYCoord = vLedPositions[iIdx][1];
			let color;

			if (overrideColor) {
				color = hexToRgb(overrideColor);
			} else if (LightingMode === "Forced") {
				color = hexToRgb(forcedColor);
			} else {
				color = device.color(curLedXCoord, curLedYCoord);
			}

			RGBData[iIdx*3]		= color[0];
			RGBData[iIdx*3+1]	= color[1];
			RGBData[iIdx*3+2]	= color[2];
		}

		device.send_report([0x53, 0x25, 0x03, 0x00, 0x00].concat(RGBData), packetLength);
	}
}

const MSIMonitor = new MSIMonitorProtocol();

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

/**
 * @typedef {("fans" | "lighting" | "macros")} Permission
 * @typedef {Object.<string, boolean>} UpdatedPermissions
 * @callback PermissionCallback
 * @param {UpdatedPermissions} updatedPermissions - ...
 */

/**
 * Manages permissions for a specific target partner. Tracks permission changes internally and
 * emits changed permissions to a provided callback funtion.
 * @class
 */
class PermissionsManager{
	/**
	 * Creates an instance of PermissionsManager.
	 * @constructor
	 * @param {string} partner - The name of the target for which permissions are managed.
	 *
	 * @param {PermissionCallback} callback - The callback function to be triggered when permissions are updated.
	 */
	constructor(partner, callback){
		/** @type {string} */
		this.target = partner;
		/** @type {Object.<string, boolean>} */
		this.permissions = {};
		/** @type {PermissionCallback} */
		this.callback = callback;
	}

	/**
	 * Registers the callback and initializes permissions.
	 */
	Register(){
		// Register callback. We HAVE to bind this as it's a class method.
		permissions.setCallback(this.HandlePermissionUpdate.bind(this));
		// Seed initial values
		this.HandlePermissionUpdate(permissions.permissions());
	}

	HandlePermissionUpdate(data){
		// users may not have permissions without internet so we likely want to just assume it's a success.
		const permissions = data[this.target];
		Assert.softIsDefined(permissions, `Permissions object doesn't contain: ${this.target}. Are you sure it's a valid partner?`);

		// This expects no new/removed permissions, only changes in status
		/** @type {UpdatedPermissions} */
		const changedPermissions = {};

		for(const key in permissions){
			if(permissions[key] !== this.permissions[key]){
				console.log(`Changed Permission! [${key}]: ${this.permissions[key]} -> ${permissions[key]}`);
				changedPermissions[key] = permissions[key];
			}
		}

		this.permissions = permissions ?? {};

		if(this.callback){
			this.callback(changedPermissions);
		}
	}

	/**
	 * Gets the value of a specific permission. Defaulting to true if it doesn't have a value
	 * @param {Permission} permission - The permission to check.
	 * @returns {boolean} - The value of the permission.
	 */
	GetPermission(permission){
		// Assume we have permissions if there isn't a setting for it.
		const value = this.permissions[permission] ?? true;
		//console.log(`Checking permission: [${permission}]. Result: [${value}]`);
		//console.log(this.permissions);

		return value;
	}
}

const permissionManager = new PermissionsManager("MSI", onPermissionsUpdated);
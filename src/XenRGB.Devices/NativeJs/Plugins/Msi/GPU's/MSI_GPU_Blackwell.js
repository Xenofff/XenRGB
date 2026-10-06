import {Assert} from "@SignalRGB/Errors.js";
import permissions from "@SignalRGB/permissions";

export function Name() { return "MSI Blackwell GPU"; }
export function Publisher() { return "WhirlwindFX"; }
export function Documentation(){ return "troubleshooting/MSI"; }
export function Type() { return "SMBUS"; }
export function Size() { return [1, 1]; }
export function DefaultPosition(){return [0, 0];}
export function DefaultScale(){return 2.5;}
export function LedNames() { return vLedNames; }
export function LedPositions() { return vLedPositions; }
export function DeviceType(){return "gpu";}
export function ImageUrl() { return "https://assets.signalrgb.com/devices/brands/msi/gpus/gpu.png"; }
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
singleMode:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{property:"singleMode", group:"", label:"Single zone mode", description: "Reduces Led control to a single zone. This can help with fps in games, and lowers CPU usage. May improve support for older VBios versions", type:"boolean", default:"false"},
	];
}

let vLedNames = [];
let vLedPositions = [];
let vLeds = [];

/** @param {FreeAddressBus} bus */
export function Scan(bus) {
	const FoundAddresses = [];

	  // Skip any non AMD / INTEL Busses
	  if (!bus.IsNvidiaBus()) {
		return [];
	}

	for(const MSIGPUID of new MSIGPUList().devices) {
		if(MSIGPUID.Vendor === bus.Vendor() &&
		MSIGPUID.SubVendor === bus.SubVendor() &&
		MSIGPUID.Device === bus.Product() &&
		MSIGPUID.SubDevice === bus.SubDevice()
		) {
			FoundAddresses.push(MSIGPUID.Address);
		}
	}

	return FoundAddresses;
}

export function BrandGPUList(){ return new MSIGPUList().devices; }

/** @param {UpdatedPermissions} updatedPermissions */
function onPermissionsUpdated(updatedPermissions){
	console.log(updatedPermissions);

	if(updatedPermissions["lighting"] === true){
		softwareMode();
	}
}

export function Initialize() {
	SetGPUNameFromBusIds(new MSIGPUList().devices);

	permissionManager.Register();

	if(permissionManager.GetPermission("lighting")) {
		softwareMode();
	}
}

export function Render() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	if (singleMode) {
		sendColorsSingle();
	}else {
		grabColors();
	}
}

export function Shutdown(SystemSuspending) {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;

	if (singleMode) {
		sendColorsSingle(color);
	}else {
		grabColors(color);
	}
}

export function onsingleModeChanged() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	softwareMode();
}

function grabColors(overrideColor) {
	const RGBData = [];

	for(let iIdx = 0; iIdx < vLedPositions.length; iIdx++) {
		const iPxX = vLedPositions[iIdx][0];
		const iPxY = vLedPositions[iIdx][1];
		let color;

		if(overrideColor) {
			color = hexToRgb(overrideColor);
		} else if (LightingMode === "Forced") {
			color = hexToRgb(forcedColor);
		} else {
			color = device.color(iPxX, iPxY);
		}

		RGBData[0] = 0x01;

		const iLedIdx	 = vLeds[iIdx]*5 + 1;
		RGBData[iLedIdx] = iIdx;
		RGBData[iLedIdx+1]	= color[0];
		RGBData[iLedIdx+2]	= color[1];
		RGBData[iLedIdx+3]	= color[2];
		RGBData[iLedIdx+4]	= 0x01;
	}

	bus.WriteBlock(0x04, RGBData.length, RGBData);
}

function sendColorsSingle(overrideColor) {
	let color;

	if(overrideColor) {
		color = hexToRgb(overrideColor);
	} else if (LightingMode === "Forced") {
		color = hexToRgb(forcedColor);
	} else {
		color = device.color(vLedPositions[0][0],  vLedPositions[0][1]);
	}

	CheckedWrite(0x22, 0x13); // Set static mode

	CheckedWrite(0x30, color[0]);
	CheckedWrite(0x31, color[1]);
	CheckedWrite(0x32, color[2]);

	device.pause(120);
}

function softwareMode() {
	if (singleMode) {
		CheckedWrite(0x2E, 0x00);
		CheckedWrite(0x46, 0x01); // 50 Series flags
		CheckedWrite(0x22, 0x13); // Set static mode
		CheckedWrite(0x36, 0x64); // Set brightness to 100%
		CheckedWrite(0x38, 0x00); // Set effect speed to 0
		bus.WriteByte(0x06, 0x01);
	} else {
		// per-led mode
		bus.WriteByte(0x46, 0x01);
		bus.WriteByte(0x06, 0x00);
		bus.WriteByte(0x20, 0x00);
	}

}

function buildLeds(Model) {
	vLedNames = [];
	vLedPositions = [];

	// Check if the device is in the library
	const deviceLEDLibrary = MSILEDLibrary[Model];

	if(deviceLEDLibrary) {
		console.log("Using LED Library");
		vLedNames = deviceLEDLibrary.ledsNames;
		vLedPositions = deviceLEDLibrary.ledsPositions;
		vLeds = deviceLEDLibrary.leds;

		device.setSize(deviceLEDLibrary.size);
	} else {
		console.log("Using Default LED Setup");

		for(let i = 0; i < 34; i++) {
			vLedNames.push(`LED ${i + 1}`);
			vLedPositions.push([ i, 0 ]);
			vLeds.push(i);
		}

		device.setSize([34, 1]);
	}

	device.setControllableLeds(vLedNames, vLedPositions);
}

function SetGPUNameFromBusIds(GPUList) {
	for(const GPU of GPUList) {
		if(CheckForIdMatch(bus, GPU)) {
			device.setName(GPU.Name);

			buildLeds(GPU.Model);

			break;
		}
	}
}

function CheckForIdMatch(bus, Gpu) {
	return Gpu.Vendor === bus.Vendor() &&
	Gpu.SubVendor === bus.SubVendor() &&
	Gpu.Device === bus.Product() &&
	Gpu.SubDevice === bus.SubDevice();
}

function CheckedWrite(register, byte){
	let attempts = 0;
	const maxAttempts = 4;

	while(attempts < maxAttempts){
		if(bus.WriteByte(register, byte) === 0){
			return true;
		}

		attempts++;
	}

	console.error(`Failed to write to register ${register} after ${maxAttempts} attempts.`);

	return false;
}

class GPUIdentifier {
	constructor(Vendor, SubVendor, Device, SubDevice, Address, Name, Model = "") {
		this.Vendor = Vendor;
		this.SubVendor = SubVendor;
		this.Device = Device;
		this.SubDevice = SubDevice;
		this.Address = Address;
		this.Name = Name;
		this.Model = Model;
	}
}

class MSIGPUIdentifier extends GPUIdentifier {
	constructor(device, SubDevice, Address, Name, Model = "") {
		super(0x10DE, 0x1462, device, SubDevice, Address, Name, Model);
	}
}

class NvidiaGPUDeviceIds {
	constructor() {
		this.RTX5060		 = 0x2D05;
		this.RTX5060TI       = 0x2D04;
		this.RTX5070         = 0x2F04;
		this.RTX5070TI       = 0x2C05;
		this.RTX5080		 = 0x2C02;
		this.RTX5090		 = 0x2B85;
	}
};

class MSIGPUDeviceIDs {
	constructor() {
		this.RTX5060_GAMING_TRIO			         = 0x5372;

		this.RTX5060TI_GAMING_TRIO			         = 0x5353;
		this.RTX5060TI_GAMING_TRIO_OC				 = 0x5360;
		this.RTX5060TI_GAMING_OC					 = 0x5361;
		this.RTX5060TI_VANGUARD_SOC			         = 0x5352;
		this.RTX5060TI_16G_GAMING_OC				 = 0x5354;

		this.RTX5070_GAMING_TRIO                     = 0x5322;
		this.RTX5070_VANGUARD_SOC					 = 0x5321;

		this.RTX5070TI_GAMING_TRIO			         = 0x5315;
		this.RTX5070TI_VANGUARD_SOC			         = 0x5314;
		this.RTX5070TI_MLG							 = 0x5318;

		this.RTX5080_GAMING_TRIO			         = 0x5315;
		this.RTX5080_VANGUARD_SOC			         = 0x5314;
		//this.RTX5080_SUPRIM_LIQUID_SOC				 = 0x5312; // Untested
		this.RTX5080_SUPRIM_SOC						 = 0x5313;

		this.RTX5090_GAMING_TRIO			         = 0x5303;
		//this.RTX5090_SUPRIM_LIQUID_SOC				 = 0x5300; // Untested
		this.RTX5090_SUPRIM_SOC			         	 = 0x5301;
		this.RTX5090_VANGUARD			         	 = 0x5302;
	}
}

class MSIGPUList {
	constructor() {
		const Nvidia = new NvidiaGPUDeviceIds();
		const MSIGPUIDs  = new MSIGPUDeviceIDs();
		this.devices =
		[
			new MSIGPUIdentifier(Nvidia.RTX5060,		MSIGPUIDs.RTX5060_GAMING_TRIO,			0x68, "MSI 5060 GAMING TRIO", "GAMING TRIO"),

			new MSIGPUIdentifier(Nvidia.RTX5060TI,      MSIGPUIDs.RTX5060TI_GAMING_TRIO,        0x68, "MSI 5060TI GAMING TRIO", "GAMING TRIO"),
			new MSIGPUIdentifier(Nvidia.RTX5060TI,      MSIGPUIDs.RTX5060TI_GAMING_TRIO_OC,		0x68, "MSI 5060TI GAMING TRIO OC", "GAMING TRIO"),
			new MSIGPUIdentifier(Nvidia.RTX5060TI,      MSIGPUIDs.RTX5060TI_GAMING_OC,			0x68, "MSI 5060TI GAMING OC", "GAMING TRIO"),
			new MSIGPUIdentifier(Nvidia.RTX5060TI,      MSIGPUIDs.RTX5060TI_VANGUARD_SOC,		0x68, "MSI 5060TI VANGUARD SOC", "VANGUARD"),
			new MSIGPUIdentifier(Nvidia.RTX5060TI,      MSIGPUIDs.RTX5060TI_16G_GAMING_OC,      0x68, "MSI 5060TI GAMING OC", "GAMING TRIO"),

			new MSIGPUIdentifier(Nvidia.RTX5070,     	MSIGPUIDs.RTX5070_GAMING_TRIO,          0x68, "MSI 5070 GAMING TRIO", "GAMING TRIO"),
			new MSIGPUIdentifier(Nvidia.RTX5070,		MSIGPUIDs.RTX5070_VANGUARD_SOC,			0x68, "MSI 5070 VANGUARD SOC", "VANGUARD"),

			new MSIGPUIdentifier(Nvidia.RTX5070TI,      MSIGPUIDs.RTX5070TI_GAMING_TRIO,        0x68, "MSI 5070TI GAMING TRIO", "GAMING TRIO"),
			new MSIGPUIdentifier(Nvidia.RTX5070TI,		MSIGPUIDs.RTX5070TI_VANGUARD_SOC,		0x68, "MSI 5070Ti VANGUARD SOC", "VANGUARD"),
			new MSIGPUIdentifier(Nvidia.RTX5070TI,		MSIGPUIDs.RTX5070TI_MLG,				0x68, "MSI 5070Ti MLG Edition", "GAMING TRIO"),

			new MSIGPUIdentifier(Nvidia.RTX5080,		MSIGPUIDs.RTX5080_GAMING_TRIO,			0x68, "MSI 5080 GAMING TRIO", "GAMING TRIO"),
			new MSIGPUIdentifier(Nvidia.RTX5080,		MSIGPUIDs.RTX5080_VANGUARD_SOC,			0x68, "MSI 5080 VANGUARD SOC", "VANGUARD"),
			//new MSIGPUIdentifier(Nvidia.RTX5080,		MSIGPUIDs.RTX5080_SUPRIM_LIQUID_SOC,	0x68, "MSI 5080 SUPRIM LIQUID SOC"),
			new MSIGPUIdentifier(Nvidia.RTX5080,		MSIGPUIDs.RTX5080_SUPRIM_SOC,			0x68, "MSI 5080 SUPRIM SOC", "SUPRIM SOC"),

			new MSIGPUIdentifier(Nvidia.RTX5090,		MSIGPUIDs.RTX5090_GAMING_TRIO,			0x68, "MSI 5090 GAMING TRIO", "GAMING TRIO"),
			//new MSIGPUIdentifier(Nvidia.RTX5090,		MSIGPUIDs.RTX5090_SUPRIM_LIQUID_SOC,	0x68, "MSI 5090 SUPRIM LIQUID SOC"),
			new MSIGPUIdentifier(Nvidia.RTX5090,		MSIGPUIDs.RTX5090_SUPRIM_SOC,			0x68, "MSI 5090 SUPRIM SOC", "SUPRIM SOC"),
			new MSIGPUIdentifier(Nvidia.RTX5090,		MSIGPUIDs.RTX5090_VANGUARD,				0x68, "MSI 5090 VANGUARD", "VANGUARD"),
		];
	}
}

const MSILEDLibrary = {
	"GAMING TRIO": { // Fan strips are mirrored on top and bottom
		size: [10, 2],
		leds: [
			0, 1, 2, 					3, 5,
									 4,
		],
		ledsPositions: [
			[0, 0], [1, 0], [2, 0], 					[8, 0], [9, 0],
													 [7, 1],
		],
		ledsNames: ["Logo 1", "Logo 2", "Logo 3", "Fan strip 1", "Fan strip 2", "Fan strip 3"],
	},
	"SUPRIM SOC": { // Fan V strips are mirrored and the logo is mirrored with the crystal on the side
		size: [7, 13],
		leds: [
			4, 3, 2, 1,

			15,
				 14,
					 13,
						 12,
							 11,
								 10,
							 9,
						 8,
					 7,
				 6,
			5
		],
		ledsPositions: [
			[0, 0], [1, 0], [2, 0], [3, 0],

					 [1, 2],
						 [2, 3],
							 [3, 4],
								 [4, 5],
									 [5, 6],
										 [6, 7],
									 [5, 8],
								 [4, 9],
							 [3, 10],
						 [2, 11],
					 [1, 12]
		],
		ledsNames: [
			"Logo 1", "Logo 2", "Logo 3", "Logo 4",
			"Fan strip 1", "Fan strip 2", "Fan strip 3", "Fan strip 4", "Fan strip 5", "Fan strip 6",
			"Fan strip 7", "Fan strip 8", "Fan strip 9", "Fan strip 10", "Fan strip 11", "Fan strip 12"
		],
	},
	"VANGUARD": {
		size: [19, 7],
		leds: [
													  1, 2, 3, 4, 5, 6, 7, 11, 12, 10,
																 8, 9,

							 18, 19,				22, 23,
					 15, 16, 17,				20, 21,
			13, 14,
		],
		ledsPositions: [
							 									 		 [10, 0], [11, 0], [12, 0], [13, 0], [14, 0], [15, 0], [16, 0], [17, 0], [18, 0], [16, 1],
																											 [14, 1], [15, 1],


										 		[5, 4], [6, 4],						[10, 4], [11, 4],
							 [2, 5], [3, 5], [4, 5],					 [8, 5], [9, 5],
			[0, 6], [1, 6],
		],
		ledsNames: [
			"Logo 1", "Logo 2", "Logo 3", "Logo 4", "Logo 5", "Logo 6", "Logo 7", "Logo 8", "Side logo 1", "Side logo 2",
			"Fan strip 1", "Fan strip 2",
			"Fan strip 3", "Fan strip 4", "Fan strip 5", "Fan strip 6",
			"Fan strip 7", "Fan strip 8", "Fan strip 9", "Fan strip 10", "Fan strip 11",
			"Fan strip 12", "Fan strip 13",
		],
	},
};

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
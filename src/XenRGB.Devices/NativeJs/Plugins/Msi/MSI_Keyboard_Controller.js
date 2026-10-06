import {Assert} from "@SignalRGB/Errors.js";
import permissions from "@SignalRGB/permissions";

export function Name() { return "MSI Keyboard Device"; }
export function VendorId() { return 0x0DB0; }
export function ProductId() { return Object.keys(MSIdeviceLibrary.PIDLibrary); }
export function Publisher() { return "WhirlwindFx"; }
export function Documentation(){ return "troubleshooting/MSI"; }
export function Size() { return [1, 1]; }
export function DeviceType(){ return "keyboard";}
export function Validate(endpoint) { return endpoint.interface === 1; }
export function ImageUrl() { return "https://assets.signalrgb.com/devices/default/misc/usb-drive-render.png"; }
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
*/
export function ControllableParameters(){
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
	];
}

/** @param {UpdatedPermissions} updatedPermissions */
function onPermissionsUpdated(updatedPermissions){
	console.log(updatedPermissions);

	if(updatedPermissions["lighting"] === true){
		MSI.Initialize();
	}
}

export function Initialize() {
	permissionManager.Register();
	
	if(permissionManager.GetPermission("lighting")) {
		MSI.Initialize();
	}
}

export function Render() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	MSI.sendColors();
}

export function Shutdown(SystemSuspending) {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;
	MSI.sendColors(color);
}

export class MSI_Device_Protocol {
	constructor() {
		this.Config = {
			DeviceProductID: 0x0000,
			DeviceName: "MSI Keyboard Device",
			DeviceEndpoint: { "interface": 0, "usage": 0x0001, "usage_page": 0xFF01, "collection": 0x0000 },
			LedNames: [],
			LedPositions: [],
			Leds: [],
		};
	}

	getDeviceProperties(deviceID) { return MSIdeviceLibrary.LEDLibrary[deviceID];};

	getDeviceProductId() { return this.Config.DeviceProductID; }
	setDeviceProductId(productID) { this.Config.DeviceProductID = productID; }

	getDeviceName() { return this.Config.DeviceName; }
	setDeviceName(deviceName) { this.Config.DeviceName = deviceName; }

	getDeviceEndpoint() { return this.Config.DeviceEndpoint; }
	setDeviceEndpoint(deviceEndpoint) { this.Config.DeviceEndpoint = deviceEndpoint; }

	getLedNames() { return this.Config.LedNames; }
	setLedNames(ledNames) { this.Config.LedNames = ledNames; }

	getLedPositions() { return this.Config.LedPositions; }
	setLedPositions(ledPositions) { this.Config.LedPositions = ledPositions; }

	getLeds() { return this.Config.Leds; }
	setLeds(leds) { this.Config.Leds = leds; }

	getDeviceImage() { return this.Config.image; }
	setDeviceImage(image) { this.Config.image = image; }

	Initialize() {
		//Initializing vars
		this.setDeviceProductId(device.productId());

		const DeviceProperties = this.getDeviceProperties(this.getDeviceProductId());
		this.setDeviceName(DeviceProperties.name);
		this.setDeviceEndpoint(DeviceProperties.endpoint);
		this.setLedNames(DeviceProperties.LedNames);
		this.setLedPositions(DeviceProperties.LedPositions);
		this.setLeds(DeviceProperties.Leds);
		this.setDeviceImage(DeviceProperties.image);

		device.log("Device model found: " + this.getDeviceName());
		device.setName("MSI " + this.getDeviceName());
		device.setSize(DeviceProperties.size);
		device.setControllableLeds(this.getLedNames(), this.getLedPositions());
		device.setImageFromUrl(this.getDeviceImage());
		device.set_endpoint(
			DeviceProperties.endpoint[`interface`],
			DeviceProperties.endpoint[`usage`],
			DeviceProperties.endpoint[`usage_page`],
			DeviceProperties.endpoint[`collection`]);

		this.setSoftwareMode();
	}

	setSoftwareMode() {
		device.write([0x00, 0x41, 0x80], 65); //Software mode
		device.write([0x00, 0x56, 0x20, 0x01], 65);
	}

	sendColors(overrideColor) {

		const deviceLedPositions	= this.getLedPositions();
		const deviceLeds			= this.getLeds();
		const RGBData				= [];
		let packet					= [];

		for (let iIdx = 0; iIdx < deviceLeds.length; iIdx++) {
			const iPxX = deviceLedPositions[iIdx][0];
			const iPxY = deviceLedPositions[iIdx][1];
			let color;

			if(overrideColor){
				color = hexToRgb(overrideColor);
			}else if (LightingMode === "Forced") {
				color = hexToRgb(forcedColor);
			}else {
				color = device.color(iPxX, iPxY);
			}

			RGBData[(deviceLeds[iIdx]*3)]   = color[0];
			RGBData[(deviceLeds[iIdx]*3)+1] = color[1];
			RGBData[(deviceLeds[iIdx]*3)+2] = color[2];
		}

		packet = [0x00, 0x56, 0x21, 0x01, 0x00, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x0b, 0x00, 0x0b, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x00, 0x01, 0x00, 0x00, 0x01, 0x00, 0xc1, 0x00, 0x00, 0x00, 0x00];
		packet = packet.concat(RGBData);
		packet = packet.concat([0xff, 0x00, 0x00, 0x00, 0x00, 0x18, 0x00, 0x00, 0x00, 0x50, 0x30, 0x00, 0x00]);

		device.write(packet, 65);
		device.write([0x00, 0x51, 0x28, 0x00, 0x00, 0x01], 65); //Apply
	}
}

export class deviceLibrary {
	constructor(){
		this.PIDLibrary	=	{
			0x0B41: "Vigor GK41 Dusk",
			0x0B5B: "Vigor GK50 Elite",
			0x1958: "Vigor GK71 Sonic",
			0x0B7A: "Vigor GK71 Sonic",
		};

		this.LEDLibrary	=	{
			0x0B41: {
				name: "Vigor GK41 Dusk",
				size: [1, 1],
				LedNames: ["Keyboard"],
				LedPositions: [[0, 0]],
				Leds: [0],
				endpoint : { "interface": 1, "usage": 0x0001, "usage_page": 0xFF00, "collection": 0x0000 },
				image: "https://assets.signalrgb.com/devices/brands/msi/keyboards/vigor-gk41-dusk.png"
			},
			0x0B5B: {
				name: "Vigor GK50 Elite",
				size: [1, 1],
				LedNames: ["Keyboard"],
				LedPositions: [[0, 0]],
				Leds: [0],
				endpoint : { "interface": 1, "usage": 0x0001, "usage_page": 0xFF00, "collection": 0x0000 },
				image: "https://assets.signalrgb.com/devices/brands/msi/keyboards/vigor-gk50-elite.png"
			},
			0x1958: {
				name: "Vigor GK71 Sonic",
				size: [1, 1],
				LedNames: ["Keyboard"],
				LedPositions: [[0, 0]],
				Leds: [0],
				endpoint : { "interface": 1, "usage": 0x0001, "usage_page": 0xFF00, "collection": 0x0000 },
				image: "https://assets.signalrgb.com/devices/brands/msi/keyboards/vigor-gk71-sonic.png"
			},
			0x0B7A: {
				name: "Vigor GK71 Sonic", //Second Varient
				size: [1, 1],
				LedNames: ["Keyboard"],
				LedPositions: [[0, 0]],
				Leds: [0],
				endpoint : { "interface": 1, "usage": 0x0001, "usage_page": 0xFF00, "collection": 0x0000 },
				image: "https://assets.signalrgb.com/devices/brands/msi/keyboards/vigor-gk71-sonic.png"
			}
		};
	}
}

const MSIdeviceLibrary = new deviceLibrary();
const MSI = new MSI_Device_Protocol();

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
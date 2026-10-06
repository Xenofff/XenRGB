export function Name() { return "HyperX Pulsefire 2"; }
export function VendorId() { return 0x03F0; }
export function ProductId() { return Object.keys(HyperXdeviceLibrary.PIDLibrary); }
export function Publisher() { return "WhirlwindFx"; }
export function Documentation(){ return "troubleshooting/HyperX"; }
export function Size() { return [1, 1]; }
export function DefaultPosition(){return [0, 0];}
export function DefaultScale(){return 1.0;}
export function ConflictingProcesses() { return ["NGenuity2.exe"]; }
export function DeviceType(){return "mouse"}
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
DpiControl:readonly
dpi1:readonly
dpi2:readonly
dpi3:readonly
dpi4:readonly
dpi5:readonly
dpi1Color:readonly
dpi2Color:readonly
dpi3Color:readonly
dpi4Color:readonly
dpi5Color:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{"property":"DpiControl", "group":"mouse", "label":"Enable Dpi Control", description: "SignalRGB will not attempt to set mouse settings like DPI and Polling Rate while this is disabled", "type":"boolean", "default":"false"},
		{"property":"dpi1", "group":"mouse", "label":"DPI 1", "step":"50", "type":"number", "min":"200", "max":"26000", "default":"400", "live" : false},
		{"property":"dpi2", "group":"mouse", "label":"DPI 2", "step":"50", "type":"number", "min":"200", "max":"26000", "default":"800", "live" : false},
		{"property":"dpi3", "group":"mouse", "label":"DPI 3", "step":"50", "type":"number", "min":"200", "max":"26000", "default":"1600", "live" : false},
		{"property":"dpi4", "group":"mouse", "label":"DPI 4", "step":"50", "type":"number", "min":"200", "max":"26000", "default":"2400", "live" : false},
		{"property":"dpi5", "group":"mouse", "label":"DPI 5", "step":"50", "type":"number", "min":"200", "max":"26000", "default":"3200", "live" : false},
		{"property":"dpi1Color", "group":"mouse", "label":"DPI 1 Color", "min":"0", "max":"360", "type":"color", "default":"#1FACED"},
		{"property":"dpi2Color", "group":"mouse", "label":"DPI 2 Color", "min":"0", "max":"360", "type":"color", "default":"#2FACED"},
		{"property":"dpi3Color", "group":"mouse", "label":"DPI 3 Color", "min":"0", "max":"360", "type":"color", "default":"#3FACED"},
		{"property":"dpi4Color", "group":"mouse", "label":"DPI 4 Color", "min":"0", "max":"360", "type":"color", "default":"#4FACED"},
		{"property":"dpi5Color", "group":"mouse", "label":"DPI 5 Color", "min":"0", "max":"360", "type":"color", "default":"#5FACED"},
	];
}

export function Initialize() {
	HyperX.InitializeHyperX();
}

export function Render() {
	HyperX.sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	HyperX.sendColors(color);
}

export class HyperX_Device_Protocol {
	constructor() {
		this.Config = {
			DeviceProductID: 0x0000,
			DeviceName: "HyperX Haste 2",
			DeviceEndpoint: { "interface": 2, "usage": 0x0001, "usage_page": 0xFF00, "collection": 0x0000 },
			LedNames: [],
			LedPositions: [],
			Leds: [],
		};
	}

	getDeviceProperties(deviceName) { return HyperXdeviceLibrary.LEDLibrary[deviceName];};

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

	InitializeHyperX() {
		//Initializing vars
		this.setDeviceProductId(device.productId());
		this.setDeviceName(HyperXdeviceLibrary.PIDLibrary[this.getDeviceProductId()]);

		const DeviceProperties = this.getDeviceProperties(this.getDeviceName());
		this.setDeviceEndpoint(DeviceProperties.endpoint);
		this.setLedNames(DeviceProperties.vLedNames);
		this.setLedPositions(DeviceProperties.vLedPositions);
		this.setLeds(DeviceProperties.vLeds);
		this.setDeviceImage(DeviceProperties.image);

		device.log(`Device model found: ` + this.getDeviceName());
		device.setName("HyperX " + this.getDeviceName());
		device.setSize(DeviceProperties.size);
		device.setControllableLeds(this.getLedNames(), this.getLedPositions());
		device.setImageFromUrl(this.getDeviceImage());
		device.set_endpoint(
			DeviceProperties.endpoint[`interface`],
			DeviceProperties.endpoint[`usage`],
			DeviceProperties.endpoint[`usage_page`],
			DeviceProperties.endpoint[`collection`]
		);

		if (DpiControl) {
			this.setDPI();
		}
	}

	sendColors(overrideColor) {

		const deviceLedPositions	= this.getLedPositions();
		const deviceLeds			= this.getLeds();
		const RGBData				= [];
		const packet				= [];

		for (let iIdx = 0; iIdx < deviceLedPositions.length; iIdx++) {
			const iPxX = deviceLedPositions[iIdx][0];
			const iPxY = deviceLedPositions[iIdx][1];
			let color;

			if(overrideColor){
				color = hexToRgb(overrideColor);
			}else if (LightingMode === "Forced") {
				color = hexToRgb(forcedColor);
			}else{
				color = device.color(iPxX, iPxY);
			}

			RGBData[(deviceLeds[iIdx]*3)]   = color[0];
			RGBData[(deviceLeds[iIdx]*3)+1] = color[1];
			RGBData[(deviceLeds[iIdx]*3)+2] = color[2];
		}

		packet[0] = 0x44;
		packet[1] = 0x02;
		packet[2] = 0x00;
		packet[3] = 0x00;

		packet.push(...RGBData);

		device.write([0x44, 0x01, 0x01], 65); // Primer
		device.write(packet, 65);
	}

	setDPI() {
		let packet = [];
		const dpi1Col = hexToRgb(dpi1Color);
		const dpi2Col = hexToRgb(dpi2Color);
		const dpi3Col = hexToRgb(dpi3Color);
		const dpi4Col = hexToRgb(dpi4Color);
		const dpi5Col = hexToRgb(dpi5Color);

		packet = [0x32, 0x01, 0x01, 0x00, 0x08, 0x1F, 0x04,
			(dpi1/50)-1, 0x00, dpi1Col[0], dpi1Col[1], dpi1Col[2],
			(dpi2/50)-1, 0x00, dpi2Col[0], dpi2Col[1], dpi2Col[2],
			(dpi3/50)-1, 0x00, dpi3Col[0], dpi3Col[1], dpi3Col[2],
			(dpi4/50)-1, 0x00, dpi4Col[0], dpi4Col[1], dpi4Col[2],
			(dpi5/50)-1, 0x00, dpi5Col[0], dpi5Col[1], dpi5Col[2]];

		device.write(packet, 65);
	}
}

export class deviceLibrary {
	constructor(){
		this.PIDLibrary	=	{
			0x0B97: "Pulsefire Haste 2", // Wired only
			0x0D97: "Pulsefire Haste 2",
			0x0F98: "Pulsefire Haste 2", // Wireless
			0x0DA0: "Pulsefire Haste 2 Mini",
			0x0BA0: "Pulsefire Haste 2 Mini", // Wireless
		};

		this.LEDLibrary	=	{
			"Pulsefire Haste 2":
			{
				size: [3, 3],
				vLedNames: ["Scroll"],
				vLedPositions: [[1, 0]],
				vLeds: [0],
				maxDPI: 26000,
				endpoint : { "interface": 2, "usage": 0x0001, "usage_page": 0xFF00, "collection": 0x0000 },
				image: "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-haste-2.png"
			},
			"Pulsefire Haste 2 Mini":
			{
				size: [3, 3],
				vLedNames: ["Scroll"],
				vLedPositions: [[1, 0]],
				vLeds: [0],
				maxDPI: 26000,
				endpoint : { "interface": 2, "usage": 0xFF00, "usage_page": 0xFF90, "collection": 0x0000 },
				image: "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-haste-2-mini.png"
			},
		};
	}
}

const HyperXdeviceLibrary = new deviceLibrary();
const HyperX = new HyperX_Device_Protocol();

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

export function onDpiControlChanged() {
	if (DpiControl) {
		HyperX.setDPI();
	}
}

export function ondpi1Changed() {
	if (DpiControl) {
		HyperX.setDPI();
	}
}

export function ondpi2Changed() {
	if (DpiControl) {
		HyperX.setDPI();
	}
}

export function ondpi3Changed() {
	if (DpiControl) {
		HyperX.setDPI();
	}
}

export function ondpi4Changed() {
	if (DpiControl) {
		HyperX.setDPI();
	}
}

export function ondpi5Changed() {
	if (DpiControl) {
		HyperX.setDPI();
	}
}

export function Validate(endpoint) {
	return endpoint.interface === 2;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/default/misc/usb-drive-render.png";
}

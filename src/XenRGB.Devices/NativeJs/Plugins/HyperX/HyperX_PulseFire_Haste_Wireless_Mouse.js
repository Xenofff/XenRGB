export function Name() { return "HyperX Pulsefire Haste Wireless"; }
export function VendorId() { return 0x03f0; }
export function ProductId() { return [0x048E, 0x028E]; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [3, 3]; }
export function DefaultPosition() {return [225, 120]; }
export function DefaultScale(){return 15.0;}
export function DeviceType(){return "mouse"}
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
PollingRate:readonly
DpiControl:readonly
dpi1:readonly
dpi2:readonly
dpi3:readonly
dpi4:readonly
*/
export function ControllableParameters(){
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{"property":"PollingRate", "group":"mouse", "label":"Polling Rate", description: "Sets the Polling Rate of this device", "type":"combobox", "values":["125hz", "250hz", "500hz", "1000hz"], "default":"125hz"},
		{"property":"DpiControl", "group":"mouse", "label":"Enable Dpi Control", description: "SignalRGB will not attempt to set mouse settings like DPI and Polling Rate while this is disabled", "type":"boolean", "default":"false"},
		{"property":"dpi1", "group":"mouse", "label":"DPI 1", "step":"100", "type":"number", "min":"200", "max":"5000", "default":"1000", "live" : "false"},
		{"property":"dpi2", "group":"mouse", "label":"DPI 2", "step":"100", "type":"number", "min":"200", "max":"5000", "default":"1500", "live" : "false"},
		{"property":"dpi3", "group":"mouse", "label":"DPI 3", "step":"100", "type":"number", "min":"200", "max":"5000", "default":"3000", "live" : "false"},
		{"property":"dpi4", "group":"mouse", "label":"DPI 4", "step":"100", "type":"number", "min":"200", "max":"5000", "default":"5000", "live" : "false"},
	];
}

export function DeviceMessages() {
	return [
		{property: "Firmware", message:"Firmware required", tooltip: "This device requires firmware v4.1.0.6+"},
	];
}

const vLeds = [
	0, 1
];

const vLedNames = [
	"Scroll", "Scroll 2"
];

const vLedPositions = [
	[1, 0], [1, 1]
];

export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {
	console.log("Developed on firmware v4.1.0.6");
	device.write([0x00, 0x46], 64);
	device.write([0x00, 0x57, 0x12, 0x01], 64);
	device.write([0x00, 0x51], 64);

	if(DpiControl){
		setDpi(dpi1, 0);
		setDpi(dpi2, 1);
		setDpi(dpi3, 2);
		setDpi(dpi4, 3);
	}
}

export function Render() {
	sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
}

function sendColors(overrideColor) {

	// Example header packet
	const packet	= [];
	const RGBData	= [];

	for (let idx = 0; idx < vLedPositions.length; idx++) {
		const iPxX = vLedPositions[idx][0];
		const iPxY = vLedPositions[idx][1];
		let color;

		if(overrideColor){
			color = hexToRgb(overrideColor);
		}else if (LightingMode === "Forced") {
			color = hexToRgb(forcedColor);
		}else{
			color = device.color(iPxX, iPxY);
		}

		RGBData[(vLeds[idx]*3)] 	= color[0];
		RGBData[(vLeds[idx]*3)+1]	= color[1];
		RGBData[(vLeds[idx]*3)+2]	= color[2];
	}

	packet[1] = 0xD2;
	packet[4] = 0x08;

	packet.push(...RGBData);

	packet[11] = 0x64;
	device.write(packet, 64);
}

export function onDpiControlChanged() {
	setDpi(dpi1, 0);
	setDpi(dpi2, 1);
	setDpi(dpi3, 2);
	setDpi(dpi4, 3);
}

export function ondpi1Changed() {
	setDpi(dpi1, 0);
}

export function ondpi2Changed() {
	setDpi(dpi2, 1);
}

export function ondpi3Changed() {
	setDpi(dpi3, 2);
}

export function ondpi4Changed() {
	setDpi(dpi4, 3);
}

export function onPollingRateChanged() {
	setPollingRate(PollingRate);
}

function setDpi(dpi, stage) {
	const packet = [0x00, 0xD3, 0x02, stage, 0x02, Math.round(dpi/50)];
	device.write(packet, 65);
	console.log(`DPI set: ${dpi} at stage ${stage+1}`);
}

function setPollingRate(polling) {
	let packet = [];

	switch (polling) {
	case "125hz":
		packet = [0x00, 0xD0, 0x00, 0x00, 0x01, 0x00];
		break;
	case "250hz":
		packet = [0x00, 0xD0, 0x00, 0x00, 0x01, 0x01];
		break;
	case "500hz":
		packet = [0x00, 0xD0, 0x00, 0x00, 0x01, 0x02];
		break;
	case "1000hz":
		packet = [0x00, 0xD0, 0x00, 0x00, 0x01, 0x03];
		break;
	default:
		packet = [0x00, 0xD0, 0x00, 0x00, 0x01, 0x00];
		break;
	}

	device.write(packet, 65);
	console.log(`Polling Rate set: ${polling}`);
}

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

export function Validate(endpoint) {
	return endpoint.interface === 2 && endpoint.usage === 0x0001 && endpoint.usage_page === 0xFF00; //New firmware v4.1.0.6 endpoint
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-haste-wireless.png";
}
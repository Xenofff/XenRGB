export function Name() { return "HyperX PulseFire Core"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return 0x16DE; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [3, 3]; }
export function DefaultPosition() {return [225, 120]; }
export function DefaultScale(){return 15.0;}
export function DeviceType(){return "mouse"}
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
DpiControl:readonly
dpi1:readonly
*/
export function ControllableParameters(){
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{"property":"DpiControl", "group":"mouse", "label":"Enable Dpi Control", description: "SignalRGB will not attempt to set mouse settings like DPI and Polling Rate while this is disabled", "type":"boolean", "default":"false"},
		{"property":"dpi1", "group":"mouse", "label":"DPI", "step":"50", "type":"number", "min":"200", "max":"12400", "default":"800"},
	];
}

const vLedNames = [ "Logo" ];

const vLedPositions = [ [1, 1] ];

export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {
	if(DpiControl) {
		setDpi(dpi1);
	}
}

export function Render() {
	sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
}

export function ondpi1Changed() {
	setDpi(dpi1);
}

function setDpi(dpi) {
	const packet = [0x00, 0xD3, 0x02, 0x00, 0x02, Math.round(dpi/50)];
	device.write(packet, 65);
}


function sendColors(overrideColor) {
	const iX = vLedPositions[0][0];
	const iY = vLedPositions[0][1];


	let color;

	if(overrideColor) {
		color = hexToRgb(overrideColor);
	} else if (LightingMode === "Forced") {
		color = hexToRgb(forcedColor);
	} else {
		color = device.color(iX, iY);
	}
	const packet = [0x07, 0x0A, color[0], color[1], color[2]];
	packet[0x08] = 0xFF;
	device.send_report(packet, 264);
	device.pause(1);
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
	return endpoint.interface === 1  && endpoint.usage === 0x0001 && endpoint.usage_page == 0xff01;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-core.png";
}
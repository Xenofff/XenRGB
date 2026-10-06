export function Name() { return "HyperX Fury Ultra"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return 0x1705; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [8, 8]; }
export function DefaultPosition(){return [230, 100];}
export function DefaultScale(){return 3.0;}
export function DeviceType(){return "mousepad"}
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
		{"property":"dpi1", "group":"mouse", "label":"DPI", "type":"number", "min":"200", "max":"12400", "default":"800"},
	];
}

const vLedNames = [ "Led 1", "Led 2", "Led 3", "Led 4", "Led 5", "Led 6", "Led 7", "Led 8", "Led 9", "Led 10", "Led 11", "Led 12", "Led 13", "Led 14", "Led 15", "Led 16", "Led 17", "Led 18", "Led 19", "Led 20" ];

const vLedPositions =
[
	//Top Bar
	[2, 0], [3, 0], [4, 0], [5, 0], [6, 0],
	//Bottom Ring
	[1, 0], [2, 0],   [6, 0],  [7, 0],
	[0, 1],                   [7, 1],
	[0, 2],                   [7, 2],
	[0, 3],                   [7, 3],
	[0, 4], [2, 4], [4, 4], [6, 4], [7, 4],
];

const vKeymap =
[
	//Top Bar
	15, 16, 17, 18, 19,
	//Bottom Ring
	13, 14, 0, 1,
	12,         2,
	11,         3,
	10,         4,
	9, 8, 7, 6, 5,
];

export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {

}

export function Render() {
	sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
}


function StartPacket() {
	device.send_report([0x00, 0x04, 0xF2, 0x00, 0x01], 65);
}

function sendColors(overrideColor) {
	StartPacket();

	//get color data
	const red = [168];
	const green = [168];
	const blue = [168];


	for(let iIdx = 0; iIdx < vKeymap.length; iIdx++) {
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

		red[vKeymap[iIdx]] = color[0];
		green[vKeymap[iIdx]] = color[1];
		blue[vKeymap[iIdx]] = color[2];
	}

	const packet = [];
	packet[0x00]   = 0x00;

	let TotalkeyCount = 128;
	let sentKeys = 0;

	while(TotalkeyCount > 0) {

		const keys = TotalkeyCount >= 16 ? 16 : TotalkeyCount;

		for(let idx = 0; idx < keys; idx++) {
			packet[(idx * 4) + 1] = 0x81;
			packet[(idx * 4) + 2] = red[sentKeys];
			packet[(idx * 4) + 3] = green[sentKeys];
			packet[(idx * 4) + 4] = blue[sentKeys];
			TotalkeyCount--;
			sentKeys++;
		}

		device.send_report(packet, 65);
	}

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
	return endpoint.interface === 0;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/mousepads/fury-ultra.png";
}
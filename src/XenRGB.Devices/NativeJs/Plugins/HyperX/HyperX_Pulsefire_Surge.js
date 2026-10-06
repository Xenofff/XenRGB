export function Name() { return "HyperX PulseFire Surge"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return 0x16D3; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [8, 12]; }
export function DefaultPosition() {return [225, 120]; }
export function DefaultScale(){return 5.0;}
export function DeviceType(){return "mouse"}
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

const vLedNames =
[
	"Led 1", "Led 2", "Led 3", "Led 4", "Led 5", "Led 6", "Led 7", "Led 8", "Led 9", "Led 10", "Led 11", "Led 12",
	"Led 13", "Led 14", "Led 15", "Led 16", "Led 17", "Led 18", "Led 19", "Led 20",
	"Led 21", "Led 22", "Led 23", "Logo", "Led 25", "Led 26", "Led 27", "Led 28", "Led 29", "Led 30", "Led 31", "Led 32"
];

const vLedPositions =
[
	[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0],
	[0, 1], 								[7, 1],
	[0, 2], 								[7, 2],
	[0, 3], 								[7, 3],
	[0, 4], 								[7, 4],
	[0, 5], 								[7, 5],
	[0, 6], 								[7, 6],
	[1, 7], 								[6, 7],
	[1, 8], 			[3, 8], 			[6, 8],
	[1, 9], 								[6, 9],
	[1, 10], 								[6, 10],
	[2, 11], [3, 11], [4, 11]
];
const vLed =
[
	4, 3, 2, 1, 31, 30, 29, 28,
	5,                  27,
	6,                  26,
	7,                  25,
	8,                  24,
	9,                  23,
	10,                 22,
	11,                 21,
	12,       32,       20,
	13,                 19,
	14,                 18,
	15, 16, 17
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

function sendColors(overrideColor) {
	const packet = [0x07, 0x14, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x00];

	const red = new Array(32).fill(0);
	const green = new Array(32).fill(0);
	const blue = new Array(32).fill(0);

	for(let iIdx = 0; iIdx < vLed.length; iIdx++) {
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

		red[vLed[iIdx]] = color[0];
		green[vLed[iIdx]] =  color[1];
		blue[vLed[iIdx]] =  color[2];
	}

	packet.push(...red.splice(0, 32));
	packet.push(...green.splice(0, 32));
	packet.push(...blue.splice(0, 32));

	packet[108] = red[0];
	packet[109] = green[0];
	packet[110] = blue[0];

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
	return "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-surge.png";
}
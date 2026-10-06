export function Name() { return "HyperX Alloy Origins Core"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return 0x16E6; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [17, 6]; }
export function DefaultPosition(){return [10, 100];}
const DESIRED_HEIGHT = 85;
export function DefaultScale(){return Math.floor(DESIRED_HEIGHT/Size()[1]);}
export function DeviceType(){return "keyboard"}
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

const vLedNames = [
	"Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",         "Print Screen", "Scroll Lock", "Pause Break",
	"`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-_", "=+", "Backspace",                        "Insert", "Home", "Page Up",
	"Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",                               "Del", "End", "Page Down",
	"CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "ISO_#", "Enter",
	"Left Shift", "ISO_<", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "ABNT_;", "/", "Right Shift",                                  "Up Arrow",
	"Left Ctrl", "Left Win", "Left Alt", "Space", "Right Alt", "Fn", "Menu", "Right Ctrl",  "Left Arrow", "Down Arrow", "Right Arrow",
];

const vLedPositions = [
	[0, 0],    [1, 0], [2, 0], [3, 0], [4, 0],    [6, 0], [7, 0], [8, 0], [9, 0],  [10, 0], [11, 0], [12, 0], [13, 0],      [14, 0], [15, 0], [16, 0],
	[0, 1],   [1, 1], [2, 1], [3, 1], [4, 1], [5, 1], [6, 1], [7, 1], [8, 1], [9, 1], [10, 1], [11, 1], [12, 1], [13, 1],     [14, 1], [15, 1], [16, 1],
	[0, 2],    [1, 2], [2, 2], [3, 2], [4, 2], [5, 2], [6, 2], [7, 2], [8, 2], [9, 2], [10, 2], [11, 2], [12, 2], [13, 2],     [14, 2], [15, 2], [16, 2],
	[0, 3],    [1, 3], [2, 3], [3, 3], [4, 3], [5, 3], [6, 3], [7, 3], [8, 3], [9, 3], [10, 3], [11, 3],  [12, 3], [13, 3],
	[0, 4],      [1, 4], [2, 4], [3, 4], [4, 4], [5, 4], [6, 4], [7, 4], [8, 4], [9, 4], [10, 4], [11, 4], [12, 4], [13, 4],           [15, 4],
	[0, 5], [1, 5], [2, 5],                      [6, 5],                        [10, 5],   [11, 5], [12, 5], [13, 5],       [14, 5], [15, 5], [16, 5],
];

const vLeds = [
	0,   1, 2, 3, 4,   5, 6, 7, 48,    49, 50, 51, 52,    53, 54, 55,
	8, 9, 10, 11, 12, 13, 14, 15, 16, 56, 57, 58, 59,   60,   61, 62, 63,
	17, 18, 19, 20, 21, 22, 23, 24, 64, 65, 66, 67, 68, 69,   70, 71, 72,
	25, 26, 27, 28, 29, 30, 31, 32, 73, 74, 75, 76, 77, 78,
	33, 34, 35, 36, 37, 38, 39, 40, 79, 80, 81, 82, 83,  84,     85,
	41, 42, 43,     45,             86, 87, 88, 89,  90, 91, 92
];

export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {
	device.write([0x00, 0x10], 65);
	device.write([0x00, 0x15], 65);
	device.write([0x00, 0x13, 0x10, 0x00, 0x32], 65);
	device.write([0x00, 0x13, 0x20, 0x00, 0x32], 65);
	device.write([0x00, 0x13, 0x30, 0x00, 32], 65);
	device.write([0x00, 0x14, 0x01], 65);
	device.write([0x00, 0x95, 0x00, 0x00, 0x01, 0x01], 65);
	device.write([0x00, 0x15], 65);
	device.write([0x00, 0x20, 0x01], 65);

	for(let i = 1; i < 14;i++){
		device.write([0x00, 0xB0, 0x01, i, 0x3C], 65);
	}

	device.write([0x00, 0x94, 0x01, 0x00, 0x05], 65);

	for(let i = 1; i < 14;i++){
		device.write([0x00, 0xB0, 0x01, i, 0x3C], 65);
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
	//get color data
	const red = new Array(130).fill(0);
	const green = new Array(130).fill(0);
	const blue = new Array(130).fill(0);
	let RGBData = [];

	for(let iIdx = 0; iIdx < vLeds.length; iIdx++) {
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

		red[vLeds[iIdx]] = color[0];
		green[vLeds[iIdx]] = color[1];
		blue[vLeds[iIdx]] = color[2];
	}

	for(let i = 0; i < 10; i++) {
		RGBData.push(...green.splice(0, 6));
		RGBData = RGBData.concat([0x00, 0x00]);
		RGBData.push(...green.splice(0, 6));
		RGBData = RGBData.concat([0x00, 0x00]);
		RGBData.push(...red.splice(0, 6));
		RGBData = RGBData.concat([0x00, 0x00]);
		RGBData.push(...red.splice(0, 6));
		RGBData = RGBData.concat([0x00, 0x00]);
		RGBData.push(...blue.splice(0, 6));
		RGBData = RGBData.concat([0x00, 0x00]);
		RGBData.push(...blue.splice(0, 6));
		RGBData = RGBData.concat([0x00, 0x00]);
	}

	let TotalkeyCount = 128;
	let sentPackets = 0;

	while(TotalkeyCount > 0) {
		const keys = TotalkeyCount >= 20 ? 20 : TotalkeyCount;

		let packet = [0x00, 0xA2, sentPackets, 0x00, keys*3];

		packet = packet.concat(RGBData.splice(0, keys*3));
		TotalkeyCount -= keys;
		sentPackets++;
		device.write(packet, 65);
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

	return endpoint.interface === 2;
}

export function ImageUrl(){
	return "https://assets.signalrgb.com/devices/brands/hyperx/keyboards/alloy-origins-core.png";
}
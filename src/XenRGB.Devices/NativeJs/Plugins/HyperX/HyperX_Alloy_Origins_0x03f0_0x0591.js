export function Name() { return "HyperX Alloy Origins"; }
export function VendorId() { return 0x03F0; }
export function ProductId() { return 0x0591; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [21, 6]; }
export function DefaultPosition(){return [10, 100];}
const DESIRED_HEIGHT = 85;
export function DefaultScale(){return Math.floor(DESIRED_HEIGHT/Size()[1]);}
export function DeviceType(){return "keyboard"}
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
layout:readonly
*/
export function ControllableParameters(){
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{"property":"layout", "group":"", "label":"Keyboard Layout", description: "Sets the keyboards Layout for Effect Canvas RGB and Keypress Effects", "type":"combobox", "values":["ANSI", "ISO", "ABNT2"], "default":"ANSI"},
	];
}

let vLedNames = [];
let vLedPositions = [];
let vKeymap = [];

const vLedNamesANSI = [
	"Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",         "Print Screen", "Scroll Lock", "Pause Break",
	"`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-_", "=+", "Backspace",                        "Insert", "Home", "Page Up",       "NumLock", "Num /", "Num *", "Num -",  //21
	"Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",                               "Del", "End", "Page Down",         "Num 7", "Num 8", "Num 9", "Num +",    //21
	"CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter",                                                              "Num 4", "Num 5", "Num 6",             //16
	"Left Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Right Shift",                                  "Up Arrow",               "Num 1", "Num 2", "Num 3", "Num Enter", //17
	"Left Ctrl", "Left Win", "Left Alt", "Space", "Right Alt", "Fn", "Menu", "Right Ctrl",  "Left Arrow", "Down Arrow", "Right Arrow", "Num 0", "Num ."                       //13
];

const vLedPositionsANSI = [
	[0, 0],    [1, 0], [2, 0], [3, 0], [4, 0],    [6, 0], [7, 0], [8, 0], [9, 0],  [10, 0], [11, 0], [12, 0], [13, 0],   [14, 0], [15, 0], [16, 0],            //20
	[0, 1],  [1, 1], [2, 1], [3, 1], [4, 1], [5, 1], [6, 1], [7, 1], [8, 1], [9, 1], [10, 1], [11, 1], [12, 1], [13, 1],   [14, 1], [15, 1], [16, 1],   [17, 1], [18, 1], [19, 1], [20, 1], //21
	[0, 2],    [1, 2], [2, 2], [3, 2], [4, 2], [5, 2], [6, 2], [7, 2], [8, 2], [9, 2], [10, 2], [11, 2], [12, 2], [13, 2],   [14, 2], [15, 2], [16, 2],   [17, 2], [18, 2], [19, 2], [20, 3], //20
	[0, 3],    [1, 3], [2, 3], [3, 3], [4, 3], [5, 3], [6, 3], [7, 3], [8, 3], [9, 3], [10, 3], [11, 3],         [13, 3],                             [17, 3], [18, 3], [19, 3], // 17
	[0, 4],      [1, 4], [2, 4], [3, 4], [4, 4], [5, 4], [6, 4], [7, 4], [8, 4], [9, 4], [10, 4], [11, 4],         [13, 4],           [15, 4],           [17, 4], [18, 4], [19, 4], [20, 4], // 17
	[0, 5], [1, 5], [2, 5],                      [6, 5],                        [10, 5],   [11, 5],     [13, 5],    [14, 5], [15, 5], [16, 5],   [17, 5],         [19, 5],               // 13
];

const vKeymapANSI = [
	0,     12, 18, 24, 30,   36, 42, 48, 54,    60, 66, 72, 78,  84, 90, 96,
	1,  7, 13, 19, 25, 31,  37, 43, 49, 55,  61, 67, 73,    79,  85, 91, 97,   103, 109, 115, 121,
	2,   8, 14, 20, 26, 32,  38, 44, 50, 56, 62, 68, 74,    80,  86, 92, 98,   104, 110, 116, 122,
	3,     9, 15, 21, 27, 33,  39, 45, 51, 57, 63, 69,      81,                105, 111, 117,
	4,     10, 16, 22, 28,  34, 40, 46, 52, 58, 64, 77,     82,      94,       106, 112, 118, 125,
	5,  11,  17,         35,                 53,     65,    83,  89, 95, 101,  107,      119,
];

const vLedNamesISO = [
	"Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",         "Print Screen", "Scroll Lock", "Pause Break",
	"`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-_", "=+", "Backspace",                        "Insert", "Home", "Page Up",       "NumLock", "Num /", "Num *", "Num -",  //21
	"Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",                               "Del", "End", "Page Down",         "Num 7", "Num 8", "Num 9", "Num +",    //21
	"CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "#", "Enter",                                                              "Num 4", "Num 5", "Num 6",             //16
	"Left Shift", "|", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Right Shift",                                  "Up Arrow",               "Num 1", "Num 2", "Num 3", "Num Enter", //17
	"Left Ctrl", "Left Win", "Left Alt", "Space", "Right Alt", "Fn", "Menu", "Right Ctrl",  "Left Arrow", "Down Arrow", "Right Arrow", "Num 0", "Num ."                       //13
];

const vLedPositionsISO = [
	[0, 0],    [1, 0], [2, 0], [3, 0], [4, 0],    [6, 0], [7, 0], [8, 0], [9, 0],  [10, 0], [11, 0], [12, 0], [13, 0],   [14, 0], [15, 0], [16, 0],            //20
	[0, 1],  [1, 1], [2, 1], [3, 1], [4, 1], [5, 1], [6, 1], [7, 1], [8, 1], [9, 1], [10, 1], [11, 1], [12, 1], [13, 1],   [14, 1], [15, 1], [16, 1],   [17, 1], [18, 1], [19, 1], [20, 1], //21
	[0, 2],    [1, 2], [2, 2], [3, 2], [4, 2], [5, 2], [6, 2], [7, 2], [8, 2], [9, 2], [10, 2], [11, 2], [12, 2], [13, 2],   [14, 2], [15, 2], [16, 2],   [17, 2], [18, 2], [19, 2], [20, 3], //20
	[0, 3],    [1, 3], [2, 3], [3, 3], [4, 3], [5, 3], [6, 3], [7, 3], [8, 3], [9, 3], [10, 3], [11, 3], [12, 2],   [13, 3],                             [17, 3], [18, 3], [19, 3], // 17
	[0, 4],      [1, 4], [2, 4], [3, 4], [4, 4], [5, 4], [6, 4], [7, 4], [8, 4], [9, 4], [10, 4], [11, 4], [12, 4], [13, 4],           [15, 4],           [17, 4], [18, 4], [19, 4], [20, 4], // 17
	[0, 5], [1, 5], [2, 5],                      [6, 5],                        [10, 5],   [11, 5],     [13, 5],    [14, 5], [15, 5], [16, 5],   [17, 5],         [19, 5],               // 13
];

const vKeymapISO = [
	0,     12, 18, 24, 30,   36, 42, 48, 54,    60, 66, 72, 78,  84, 90, 96,
	1,  7, 13, 19, 25, 31,  37, 43, 49, 55,  61, 67, 73,    79,  85, 91, 97,   103, 109, 115, 121,
	2,   8, 14, 20, 26, 32,  38, 44, 50, 56, 62, 68, 74,    80,  86, 92, 98,   104, 110, 116, 122,
	3,     9, 15, 21, 27, 33,  39, 45, 51, 57, 63, 69, 75,  81,                105, 111, 117,
	4,  6, 10, 16, 22, 28,  34, 40, 46, 52, 58, 64, 77,     82,      94,       106, 112, 118, 125,
	5,  11,  17,         35,                 53,     65,    83,  89, 95, 101,  107,      119,
];

const vLedNamesABNT2 = [
	"Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",         "Print Screen", "Scroll Lock", "Pause Break",
	"`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-_", "=+", "Backspace",                        "Insert", "Home", "Page Up",       "NumLock", "Num /", "Num *", "Num -",  //21
	"Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",                               "Del", "End", "Page Down",         "Num 7", "Num 8", "Num 9", "Num +",    //21
	"CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", "Ç", "~", "]",  "Enter",                                                              "Num 4", "Num 5", "Num 6",             //16
	"Left Shift", "\|", "Z", "X", "C", "V", "B", "N", "M", ",", ".", ";", "/", "Right Shift",                                  "Up Arrow",               "Num 1", "Num 2", "Num 3", "Num Enter", //17
	"Left Ctrl", "Left Win", "Left Alt", "Space", "Right Alt", "Fn", "Menu", "Right Ctrl",  "Left Arrow", "Down Arrow", "Right Arrow", "Num 0", "Num ."                       //13
];

const vLedPositionsABNT2 = [
	[0, 0],    [1, 0], [2, 0], [3, 0], [4, 0],    [6, 0], [7, 0], [8, 0], [9, 0],  [10, 0], [11, 0], [12, 0], [13, 0],   [14, 0], [15, 0], [16, 0],            //20
	[0, 1],  [1, 1], [2, 1], [3, 1], [4, 1], [5, 1], [6, 1], [7, 1], [8, 1], [9, 1], [10, 1], [11, 1], [12, 1], [13, 1],   [14, 1], [15, 1], [16, 1],   [17, 1], [18, 1], [19, 1], [20, 1], //21
	[0, 2],    [1, 2], [2, 2], [3, 2], [4, 2], [5, 2], [6, 2], [7, 2], [8, 2], [9, 2], [10, 2], [11, 2], [12, 2], [13, 2],   [14, 2], [15, 2], [16, 2],   [17, 2], [18, 2], [19, 2], [20, 3], //20
	[0, 3],    [1, 3], [2, 3], [3, 3], [4, 3], [5, 3], [6, 3], [7, 3], [8, 3], [9, 3], [10, 3], [11, 3], [12, 3],         [14, 3],                             [18, 3], [19, 3], [20, 3], // 17
	[0, 4], [1, 4],  [2, 4], [3, 4], [4, 4], [5, 4], [6, 4], [7, 4], [8, 4], [9, 4], [10, 4], [11, 4], [12, 4], [13, 4],          [14, 4],           [16, 4],           [18, 4], [19, 4], [20, 4], [21, 4], // 17
	[0, 5], [1, 5], [2, 5],                      [6, 5],                        [10, 5],   [11, 5],     [13, 5],    [14, 5], [15, 5], [16, 5],   [17, 5],         [19, 5],               // 13
];

const vKeymapABNT2 = [
	0,     12, 18, 24, 30,   36, 42, 48, 54,    60, 66, 72, 78,  84, 90, 96,
	1,  7, 13, 19, 25, 31,  37, 43, 49, 55,  61, 67, 73,    79,  85, 91, 97,   103, 109, 115, 121,
	2,   8, 14, 20, 26, 32,  38, 44, 50, 56, 62, 68, 74,    80,  86, 92, 98,   104, 110, 116, 122,
	3,     9, 15, 21, 27, 33,  39, 45, 51, 57, 63, 69,  75,  81,                105, 111, 117,
	4,   6,  10, 16, 22, 28,  34, 40, 46, 52, 58, 64, 77,  76,  82,      94,       106, 112, 118, 125,
	5,  11,  17,         35,                 53,     65,    83,  89, 95, 101,  107,      119,
];

export function LedNames() {
	return vLedNamesANSI;
}

export function LedPositions() {
	return vLedPositionsANSI;
}

export function onlayoutChanged() {
	if (layout === "ANSI") {
		vLedNames = vLedNamesANSI;
		vLedPositions = vLedPositionsANSI;
		vKeymap = vKeymapANSI;
	} else if (layout === "ISO") {
		vLedNames = vLedNamesISO;
		vLedPositions = vLedPositionsISO;
		vKeymap = vKeymapISO;
	} else {
		vLedNames = vLedNamesABNT2;
		vLedPositions = vLedPositionsABNT2;
		vKeymap = vKeymapABNT2;
	}

	device.log(`Layout changed to ${layout}`);
	device.setControllableLeds(vLedNames, vLedPositions);
}

export function Initialize() {
	onlayoutChanged();
}

export function Render() {
	sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
}

function StartPacket() {
	device.send_report([0x00, 0x04, 0xF2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09], 65);
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
	return endpoint.interface === 3;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/keyboards/alloy-origins.png";
}

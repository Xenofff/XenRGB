export function Name() { return "HyperX Alloy FPS RGB"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return 0x16DC; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [21, 6]; }
export function DeviceType(){return "keyboard";}
export function Validate(endpoint) { return endpoint.interface === 2 && endpoint.usage === 1 && endpoint.usage_page === 0xff01; }
export function ImageUrl() { return "https://assets.signalrgb.com/devices/brands/hyperx/keyboards/alloy-fps-rgb.png"; }
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
	"Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",         "Print Screen", "Scroll Lock", "Pause Break",
	"`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-_", "=+", "Backspace",                        "Insert", "Home", "Page Up",       "NumLock", "Num /", "Num *", "Num -",  //21
	"Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",                               "Del", "End", "Page Down",         "Num 7", "Num 8", "Num 9", "Num +",    //21
	"CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter",                                                              "Num 4", "Num 5", "Num 6",             //16
	"Left Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Right Shift",                                  "Up Arrow",               "Num 1", "Num 2", "Num 3", "Num Enter", //17
	"Left Ctrl", "Left Win", "Left Alt", "Space", "Right Alt", "Fn", "Menu", "Right Ctrl",  "Left Arrow", "Down Arrow", "Right Arrow", "Num 0", "Num .",                       //13
	"ISO_<", "ISO_#"
];


const vLedPositions =
[
	[0, 0], [1, 0], [2, 0], [3, 0], [4, 0],    [6, 0], [7, 0], [8, 0], [9, 0],  [10, 0], [11, 0], [12, 0], [13, 0],           [14, 0], [15, 0], [16, 0],            //20
	[0, 1],  [1, 1], [2, 1], [3, 1], [4, 1], [5, 1], [6, 1], [7, 1], [8, 1], [9, 1], [10, 1], [11, 1], [12, 1], [13, 1],     [14, 1], [15, 1], [16, 1],   [17, 1], [18, 1], [19, 1], [20, 1], //21
	[0, 2],   [1, 2], [2, 2], [3, 2], [4, 2], [5, 2], [6, 2], [7, 2], [8, 2], [9, 2], [10, 2], [11, 2], [12, 2], [13, 2],   [14, 2], [15, 2], [16, 2],   [17, 2], [18, 2], [19, 2], [20, 3], //20
	[0, 3],    [1, 3], [2, 3], [3, 3], [4, 3], [5, 3], [6, 3], [7, 3], [8, 3], [9, 3], [10, 3], [11, 3],         [13, 3],                             [17, 3], [18, 3], [19, 3], // 17
	[0, 4],    [2, 4], [3, 4], [4, 4], [5, 4], [6, 4], [7, 4], [8, 4], [9, 4], [10, 4], [11, 4],                 [13, 4],        [15, 4],           [17, 4], [18, 4], [19, 4], [20, 4], // 17
	[0, 5], [1, 5], [2, 5],                      [6, 5],                        [10, 5],   [11, 5],  [12, 5],  [13, 5], [14, 5], [15, 5], [16, 5],   [17, 5],         [19, 5],               // 13
	// ISO
	[1, 4], [12, 3]

];

const vKeymap =
[

	4,    20, 36, 52, 68,   84, 100, 116, 132,    12, 28, 44, 10,  26, 42, 58,
	5, 21, 37, 53, 69, 85,  101, 117, 133, 13,  29, 45, 11,    43,  74, 90, 106,   64, 80, 96, 112,
	6,  22, 38, 54, 70, 86,  102, 118, 134, 14, 30, 46, 122,    138,  59, 75, 91,   61, 77, 93, 128,
	7,    23, 39, 55, 71, 87,  103, 119, 135, 15, 31, 47,     34,                125, 141, 65,
	8,    40, 56, 72, 88,  104, 120, 136, 16, 32,  48,     107,      139,       81, 97, 113, 144,
	9, 25,  41,         73,                 121,     137,  17,  123,  19, 35, 51,  129,      145,
	24, 18 // ISO

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

function sendColorchannel(channel, data) {
	const packet = [0x07, 0x16, channel, 0xA0];
	packet.push(...data);

	device.send_report(packet, 264);

}

function sendColors(overrideColor) {
	//get color data
	const red = new Array(106).fill(0);
	const green = new Array(106).fill(0);
	const blue = new Array(106).fill(0);


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

	sendColorchannel(1, red);
	device.pause(10);
	sendColorchannel(2, green);
	device.pause(10);
	sendColorchannel(3, blue);
	device.pause(10);

	device.pause(10);
}

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

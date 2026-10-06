export function Name() { return "HyperX Alloy Elite RGB"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return 0x16be; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [21, 8]; }
export function DefaultPosition(){return [50, 100];}
export function DefaultScale(){return 8.0;}
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

const vLedNames =
[
	"MediaPreviousTrack", "MediaPlayPause", "MediaNextTrack", "VOLUME_MUTE",
	"LightBar Led 1", "LightBar Led 2", "LightBar Led 3", "LightBar Led 4", "LightBar Led 5", "LightBar Led 6", "LightBar Led 7", "LightBar Led 8", "LightBar Led 9", "LightBar Led 10",
	"LightBar Led 11", "LightBar Led 12", "LightBar Led 13", "LightBar Led 14", "LightBar Led 15", "LightBar Led 16", "LightBar Led 17", "LightBar Led 18",


	"Esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",         "Print Screen", "Scroll Lock", "Pause Break",
	"`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-_", "=+", "Backspace",                        "Insert", "Home", "Page Up",       "NumLock", "Num /", "Num *", "Num -",  //21zxf
	"Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\",                               "Del", "End", "Page Down",         "Num 7", "Num 8", "Num 9", "Num +",    //21
	"CapsLock", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter",                                                              "Num 4", "Num 5", "Num 6",             //16
	"Left Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Right Shift",                                  "Up Arrow",               "Num 1", "Num 2", "Num 3", "Num Enter", //17
	"Left Ctrl", "Left Win", "Left Alt", "Space", "Right Alt", "Fn", "Menu", "Right Ctrl",  "Left Arrow", "Down Arrow", "Right Arrow", "Num 0", "Num ."                       //13
];

const vLedPositions =
[                                                                                                                                     [15, 0], [16, 0], [17, 0], [18, 0],
	[0, 1], [1, 1], [2, 1],        [4, 1], [5, 1], [6, 1],        [8, 1], [9, 1], [10, 1], [11, 1], [12, 1], [13, 1],   [14, 1], [15, 1], [16, 1],   [17, 1], [18, 1], [19, 1],

	[0, 2], [1, 2], [2, 2], [3, 2], [4, 2],        [6, 2], [7, 2], [8, 2], [9, 2], [10, 1], [11, 1], [12, 2], [13, 2],   [14, 2], [15, 2], [16, 2],            //20
	[0, 3], [1, 3], [2, 3], [3, 3], [4, 3], [5, 3], [6, 3], [7, 3], [8, 3], [9, 3], [10, 3], [11, 3], [12, 3], [13, 3],   [14, 3], [15, 3], [16, 3],   [17, 1], [18, 3], [19, 3], [20, 3], //21
	[0, 4], [1, 4], [2, 4], [3, 4], [4, 4], [5, 4], [6, 4], [7, 4], [8, 4], [9, 4], [10, 4], [11, 4], [12, 4], [13, 4],   [14, 4], [15, 4], [16, 4],   [17, 4], [18, 4], [19, 4], [20, 4], //20
	[0, 5], [1, 5], [2, 5], [3, 5], [4, 5], [5, 5], [6, 5], [7, 5], [8, 5], [9, 5], [10, 5], [11, 5],         [13, 5],                             [17, 5], [18, 5], [19, 5], // 17
	[0, 6], [1, 6], [2, 6], [3, 6], [4, 6], [5, 6], [6, 6], [7, 6], [8, 6], [9, 6], [10, 6],                 [13, 6],           [15, 6],           [17, 6], [18, 6], [19, 6], [20, 6], // 17
	[0, 7], [1, 7], [2, 7],                      [6, 7],                      [10, 7], [11, 7], [12, 7], [13, 7],    [14, 7], [15, 7], [16, 7],   [17, 7],         [19, 7],               // 13
];

const vKeymap =
[
	//Media Keys 115
	168, 169, 170, 171,
	//Light Bar

	150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167,
	//
	4,    20, 36, 52, 68,   84, 100, 116, 132,    12, 28, 44, 10,  26, 42, 58,
	5, 21, 37, 53, 69, 85,  101, 117, 133, 13,  29, 45, 11,    43,  74, 90, 106,   64, 80, 96, 112,
	6,  22, 38, 54, 70, 86,  102, 118, 134, 14, 30, 46, 122,    138,  59, 75, 91,   61, 77, 93, 128,
	7,    23, 39, 55, 71, 87,  103, 119, 135, 15, 31, 47,     34,                125, 141, 65,
	8,    40, 56, 72, 88,  104, 120, 136, 16, 32,  48,     107,      139,       81, 97, 113, 144,
	9, 25,  41,         73,                 121,     137,  17,  123,  19, 35, 51,  129,      145,
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
	SendBar();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
	SendBar(color);
}

function sendColorchannel(channel, data) {
	const packet = [0x07, 0x16, channel, 0xA0];
	packet.push(...data);

	device.send_report(packet, 264);
	device.pause(1);
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

const Bar_Red =  [ 0x92, 0x13, 0x93, 0x12, 0x08, 0x48, 0x88, 0x09, 0x89, 0x0A, 0x8A, 0x0B, 0x8B, 0x0C, 0x8C, 0x0D, 0x8D, 0x0E, 0x8F, 0x8E, 0x0F, 0x4F ];
const Bar_Green =  [ 0x82, 0x23, 0x83, 0x22, 0x29, 0x28, 0x78, 0x19, 0x79, 0x1A, 0x7A, 0x1B, 0x7B, 0x1C, 0x7C, 0x1D, 0x7D, 0x1E, 0x6E, 0x7E, 0x1F, 0x6F ];
const Bar_Blue = [ 0x72, 0x33, 0x73, 0x32, 0x39, 0x38, 0x68, 0x3A, 0x69, 0x2A, 0x6A, 0x2B, 0x6B, 0x2C, 0x6C, 0x2D, 0x6D, 0x2E, 0x5E, 0x5D, 0x2F, 0x5F ];

function SendBar(overrideColor) //Sends Lightbar and Media Keys
{
	const packet = [0x07, 0x16, 0x04, 0x0A];

	for(let iIdx = 0; iIdx < 22; iIdx++) {
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

		packet[Bar_Red[iIdx]] = color[0];
		packet[Bar_Green[iIdx]] = color[1];
		packet[Bar_Blue[iIdx]] = color[2];
	}

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
	return endpoint.interface === 2 && endpoint.usage === 1 && endpoint.usage_page === 0xff01;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/keyboards/alloy-elite.png";
}
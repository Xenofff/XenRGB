export function Name() { return "HyperX Quadcast 2 S"; }
export function VendorId() { return 0x03f0; }
export function ProductId() { return 0x02B5; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [12, 9]; }
export function DefaultPosition() {return [150, 75]; }
export function DefaultScale(){return 8.0;}
export function DeviceType(){return "microphone";}
/* global
LightingMode:readonly
forcedColor:readonly
*/
export function ControllableParameters(){
	return [
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
	];
}

const vLedNames = [
	"Led1", "Led2", "Led3", "Led4", "Led5", "Led6", "Led7", "Led8", "Led9", "Led10", "Led11", "Led12",
	"Led13", "Led14", "Led15", "Led16", "Led17", "Led18", "Led19", "Led20", "Led21", "Led22", "Led23", "Led24",
	"Led25", "Led26", "Led27", "Led28", "Led29", "Led30", "Led31", "Led32", "Led33", "Led34", "Led35", "Led36",
	"Led37", "Led38", "Led39", "Led40", "Led41", "Led42", "Led43", "Led44", "Led45", "Led46", "Led47", "Led48",
	"Led49", "Led50", "Led51", "Led52", "Led53", "Led54", "Led55", "Led56", "Led57", "Led58", "Led59", "Led60",
	"Led61", "Led62", "Led63", "Led64", "Led65", "Led66", "Led67", "Led68", "Led69", "Led70", "Led71", "Led72",
	"Led73", "Led74", "Led75", "Led76", "Led77", "Led78", "Led79", "Led80", "Led81", "Led82", "Led83", "Led84",
	"Led85", "Led86", "Led87", "Led88", "Led89", "Led90", "Led91", "Led92", "Led93", "Led94", "Led95", "Led96",
	"Led97", "Led98", "Led99", "Led100", "Led101", "Led102", "Led103", "Led104", "Led105", "Led106", "Led107", "Led108"
];

const vLedPositions = [
	[ 0, 0 ], [ 1, 0 ], [ 2, 0 ], [ 3, 0 ], [ 4, 0 ], [ 5, 0 ], [ 6, 0 ], [ 7, 0 ], [ 8, 0 ], [ 9, 0 ], [ 10, 0 ], [ 11, 0 ],
	[ 0, 1 ], [ 1, 1 ], [ 2, 1 ], [ 3, 1 ], [ 4, 1 ], [ 5, 1 ], [ 6, 1 ], [ 7, 1 ], [ 8, 1 ], [ 9, 1 ], [ 10, 1 ], [ 11, 1 ],
	[ 0, 2 ], [ 1, 2 ], [ 2, 2 ], [ 3, 2 ], [ 4, 2 ], [ 5, 2 ], [ 6, 2 ], [ 7, 2 ], [ 8, 2 ], [ 9, 2 ], [ 10, 2 ], [ 11, 2 ],
	[ 0, 3 ], [ 1, 3 ], [ 2, 3 ], [ 3, 3 ], [ 4, 3 ], [ 5, 3 ], [ 6, 3 ], [ 7, 3 ], [ 8, 3 ], [ 9, 3 ], [ 10, 3 ], [ 11, 3 ],
	[ 0, 4 ], [ 1, 4 ], [ 2, 4 ], [ 3, 4 ], [ 4, 4 ], [ 5, 4 ], [ 6, 4 ], [ 7, 4 ], [ 8, 4 ], [ 9, 4 ], [ 10, 4 ], [ 11, 4 ],
	[ 0, 5 ], [ 1, 5 ], [ 2, 5 ], [ 3, 5 ], [ 4, 5 ], [ 5, 5 ], [ 6, 5 ], [ 7, 5 ], [ 8, 5 ], [ 9, 5 ], [ 10, 5 ], [ 11, 5 ],
	[ 0, 6 ], [ 1, 6 ], [ 2, 6 ], [ 3, 6 ], [ 4, 6 ], [ 5, 6 ], [ 6, 6 ], [ 7, 6 ], [ 8, 6 ], [ 9, 6 ], [ 10, 6 ], [ 11, 6 ],
	[ 0, 7 ], [ 1, 7 ], [ 2, 7 ], [ 3, 7 ], [ 4, 7 ], [ 5, 7 ], [ 6, 7 ], [ 7, 7 ], [ 8, 7 ], [ 9, 7 ], [ 10, 7 ], [ 11, 7 ],
	[ 0, 8 ], [ 1, 8 ], [ 2, 8 ], [ 3, 8 ], [ 4, 8 ], [ 5, 8 ], [ 6, 8 ], [ 7, 8 ], [ 8, 8 ], [ 9, 8 ], [ 10, 8 ], [ 11, 8 ],
];

const vLeds = [
	8,  9, 26, 27, 44, 45, 62, 63, 80, 81, 98, 99,
	7, 10, 25, 28, 43, 46, 61, 64, 79, 82, 97, 100,
	6, 11, 24, 29, 42, 47, 60, 65, 78, 83, 96, 101,
	5, 12, 23, 30, 41, 48, 59, 66, 77, 84, 95, 102,
	4, 13, 22, 31, 40, 49, 58, 67, 76, 85, 94, 103,
	3, 14, 21, 32, 39, 50, 57, 68, 75, 86, 93, 104,
	2, 15, 20, 33, 38, 51, 56, 69, 74, 87, 92, 105,
	1, 16, 19, 34, 37, 52, 55, 70, 73, 88, 91, 106,
	0, 17, 18, 35, 36, 53, 54, 71, 72, 89, 90, 107
];

export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {
	device.write([0x10, 0x01], 64);
}

export function Render() {
	sendColors();
}

export function Shutdown(SystemSuspending) {
	//Do nothing. Device reverts to hardware mode under some circumstances when closing srgb.
}

function grabColors(overrideColor) {
	const RGBData = [];

	for(let iIdx = 0; iIdx < vLedPositions.length; iIdx++) {
		const iPxX = vLedPositions[iIdx][0];
		const iPxY = vLedPositions[iIdx][1];
		let color;

		if(overrideColor) {
			color = hexToRgb(overrideColor);
		}else if (LightingMode === "Forced") {
			color = hexToRgb(forcedColor);
		}else {
			color = device.color(iPxX, iPxY);
		}

		RGBData[vLeds[iIdx] * 3] = color[0];
		RGBData[vLeds[iIdx] * 3 + 1] = color[1];
		RGBData[vLeds[iIdx] * 3 + 2] = color[2];
	}

	return RGBData;
}

function sendColors(overrideColor) {
	const RGBData = grabColors(overrideColor);

	for(let packets = 0; packets < 6; packets++) {
		const packet = [0x44, 0x02, packets, 0x00].concat(RGBData.splice(0, 60));
		device.write(packet, 64);
	}

	device.write([0x44, 0x01, 0x06], 64);
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
	return endpoint.interface === 1 && endpoint.usage === 0xff00;
}

export function ImageUrl(){
	return "https://assets.signalrgb.com/devices/brands/hyperx/audio/quadcast-2s.png";
}
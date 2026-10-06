export function Name() { return "HyperX Pulsefire Haste"; }
export function VendorId() { return 0x03f0; }
export function ProductId() { return 0x0f8f; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [3, 3]; }
export function DefaultPosition() {return [225, 120]; }
export function DefaultScale(){return 15.0;}
export function DeviceType(){return "mouse";}
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

const vLedNames = [ "Scroll" ];

const vLedPositions = [ [1, 0] ];


export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {
	device.send_report([0x00, 0x04, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01], 65);
	device.pause(10);
	device.get_report([0x00], 65);

	sendApplyPrimerPacket();
}

function sendApplyPrimerPacket() {
	const packet = [0x00, 0x04, 0x02];
	packet[63] = 0xaa;
	packet[64] = 0x55;
	device.send_report(packet, 65);
	device.pause(10);
	device.get_report([0x00], 65);
}

export function Render() {
	colorPrimer();
	sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
}

function colorPrimer() {
	device.send_report([0x00, 0x04, 0xf2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02], 65);
	device.get_report([0x00], 65);
}

function sendColors(overrideColor) {
	const iPxX = vLedPositions[0][0];
	const iPxY = vLedPositions[0][1];
	let color;

	if(overrideColor) {
		color = hexToRgb(overrideColor);
	} else if (LightingMode === "Forced") {
		color = hexToRgb(forcedColor);
	} else {
		color = device.color(iPxX, iPxY);
	}

	const packet = [0x00, 0x81, color[0], color[1], color[2]];

	packet[9] = 0x02;
	device.send_report(packet, 65);
	device.get_report([0x00], 65);
}

//AH YES THIS IS VERY CONSISTENT MATH
const skipTable = [
	0, 1400, 2600, 3800, 4400, 4900, 5500, 5900, 6700, 8200,
	8700, 9100, 9700, 10200, 10700, 11200, 11700, 12800, 13900, 14400, 14900, 15400, 15800
];

function getCorrectedDpiVal(dpi) {
	const closest = Math.max(...skipTable.filter(num => num <= dpi));
	const stepsToSkip = skipTable.indexOf(closest);
	const correctedDpiVal = (dpi/100) + stepsToSkip;

	return correctedDpiVal;
}

function setDpi() {
	const packet = [0x00, 0x03];
	packet[13] = 0xff;
	packet[18] = 0x0f;
	//packet[21] = getCorrectedDpiVal(dpi1);
	//packet[23] = getCorrectedDpiVal(dpi2);
	//packet[25] = getCorrectedDpiVal(dpi3);
	//packet[27] = getCorrectedDpiVal(dpi4);
	//packet[29] = getCorrectedDpiVal(dpi5);
	packet[33] = 0xff;//first level
	packet[37] = 0xff;//second level
	packet[41] = 0xff;//third level
	packet[45] = 0xff;//fourth level
	packet[49] = 0xff;//fifth level
	packet[63] = 0xaa;
	packet[64] = 0x55;

	device.send_report(packet, 65);
	device.get_report([0x00], 65);
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
	return "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-haste.png";
}
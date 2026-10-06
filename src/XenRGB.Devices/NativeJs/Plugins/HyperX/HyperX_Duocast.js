export function Name() { return "HyperX Duocast"; }
export function VendorId() { return 0x03f0; }
export function ProductId() { return 0x098C; }
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [3, 2]; }
export function DefaultPosition() {return [150, 75]; }
export function DefaultScale(){return 8.0;}
export function DeviceType(){return "microphone"}
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

const vLedNames = [ "Top Ring", "Bottom Ring" ];

const vLedPositions = [ [0, 0], [2, 0] ];

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
	device.send_report([0x00, 0x04, 0xF2], 65);
}

function sendColors(overrideColor) {
	StartPacket(); //This device doesn't seem to actually care if we send this, but it's sent with every update through NGenuity.


	const packet = [0x00];

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

		const idx = iIdx * 4;

		packet[idx + 1] = 0x81;
		packet[idx + 2] = color[0];
		packet[idx + 3] = color[1];
		packet[idx + 4] = color[2];
	}

	device.send_report(packet, 65);

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

export function ImageUrl(){
	return "https://assets.signalrgb.com/devices/brands/hyperx/audio/duocast.png";
}
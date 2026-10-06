export function Name() { return "HyperX PulseFire Mat"; }
export function VendorId() { return 0x03f0; }
export function ProductId() { return 0x0f8d; }
export function Publisher() { return "Darkest#3270 & HarD#9999"; }
export function Size() { return [16, 7]; }
export function DefaultPosition() { return [70, 135]; }
export function DefaultScale() { return 20.0; }
export function DeviceType(){return "mousepad"}
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
	"Bottom Left", "Top Right",
];

const vLedPositions = [
	[0, 6], [15, 0]
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

function StartPacket(){
	device.send_report([0x00, 0x04, 0xF2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02], 65);
}

function sendColors(overrideColor) {
	StartPacket();

	const packet = [];
	packet[0] = 0x00; //Zero Padding

	for (let idx = 0; idx < vLedPositions.length; idx++) {
		const iPxX = vLedPositions[idx][0];
		const iPxY = vLedPositions[idx][1];
		let color;

		if (overrideColor){
			color = hexToRgb(overrideColor);
		} else if (LightingMode === "Forced") {
			color = hexToRgb(forcedColor);
		} else {
			color = device.color(iPxX, iPxY);
		}

		packet[(idx * 4) + 1] = 0x81;
		packet[(idx * 4) + 2] = color[0];
		packet[(idx * 4) + 3] = color[1];
		packet[(idx * 4) + 4] = color[2];
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
	return endpoint.interface === 1 && endpoint.usage === 0xff00 && endpoint.usage_page === 0xff90;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/mousepads/pulsefire-mat.png";
}
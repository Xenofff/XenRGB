

export function Name() { return "MSI Mystic Light Controller (162 Byte)"; }
export function VendorId() { return 0x1462; }
export function Documentation(){ return "troubleshooting/msi"; }
// DO NOT PID SWAP THIS IF YOU DONT KNOW WHAT YOUR DOING
export function ProductId() { return 0x7B18;}
// YOU CAN BRICK THESE MOTHERBOARDS RGB CONTROLLER WITH ONE WRONG PACKET
export function Publisher() { return "LIGHTVORTEX"; }
export function Size() { return [10, 1]; }
export function Type() { return "Hid"; }
export function DefaultPosition(){return [0, 0];}
export function DefaultScale(){return 8.0;}
export function DeviceType(){return "motherboard"}
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
export function ConflictingProcesses(){
	return ["LedKeeper.exe", "Dragon Center", "DCv2.exe", "LightKeeperService.exe", "LightKeeperService2.exe" ];
}
const ParentDeviceName = "MSI B450";

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

const vLedNames = [
	"Mainboard Led 1", "Mainboard Led 2", "Mainboard Led 3", "Mainboard Led 4", "Mainboard Led 5",
	"Mainboard Led 6", "Mainboard Led 7", "Mainboard Led 8", "Mainboard Led 9", "Mainboard Led 10",
];
const vLedPositions = [
	[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0]
];
const vLedMap = [
	0, 1, 2, 3, 4, 5, 6, 7, 8, 9
];
const initialPacket = [
	0x52,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x80, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0xFF, 0x00, 0x80, 0x00,
	0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
	0x00,
];

const MSI_162_HEADER_OFFSET     = 0x00;
const MSI_162_JRGB1_OFFSET      = 0x01;
const MSI_162_RAINBOW1_OFFSET = 0x0B;
const MSI_162_CORSAIR_OFFSET = 0x15;
const MSI_162_MAINBOARD_1_OFFSET = 0x29;
const MSI_162_MAINBOARD_2_OFFSET = 0x33;
const MSI_162_MAINBOARD_3_OFFSET = 0x3D;
const MSI_162_MAINBOARD_4_OFFSET = 0x47;
const MSI_162_MAINBOARD_5_OFFSET = 0x51;
const MSI_162_MAINBOARD_6_OFFSET = 0x5B;
const MSI_162_MAINBOARD_7_OFFSET = 0x65;
const MSI_162_MAINBOARD_8_OFFSET = 0x6F;
const MSI_162_MAINBOARD_9_OFFSET = 0x79;
const MSI_162_MAINBOARD_10_OFFSET = 0x83;
const MSI_162_TEMP_OFFSET = 0x8D;
const MSI_162_JRGB2_OFFSET = 0x97;
const MSI_162_SAVE_FLAG = 0xA1;

const Zone_Length = 10;
const Zone_Mode_Offset = 0;
const Zone_RED_Offset = 1;
const Zone_GREEN_Offset = 2;
const Zone_BLUE_Offset = 3;
const Zone_RED_Offset1 = 5;
const Zone_GREEN_Offset1 = 6;
const Zone_BLUE_Offset1 = 7;

const mainboardDict = [
	MSI_162_MAINBOARD_1_OFFSET,
	MSI_162_MAINBOARD_2_OFFSET,
	MSI_162_MAINBOARD_3_OFFSET,
	MSI_162_MAINBOARD_4_OFFSET,
	MSI_162_MAINBOARD_5_OFFSET,
	MSI_162_MAINBOARD_6_OFFSET,
	MSI_162_MAINBOARD_7_OFFSET,
	MSI_162_MAINBOARD_8_OFFSET,
	MSI_162_MAINBOARD_9_OFFSET,
	MSI_162_MAINBOARD_10_OFFSET
];
const HeaderDict = [
	MSI_162_JRGB1_OFFSET,
	MSI_162_JRGB2_OFFSET
];
const ARGBHeaderDict = [
	MSI_162_RAINBOW1_OFFSET
];
const HeaderArray = [
	"Bottom Fans",
	"Top Fans",
];
const ARGBHeaderArray = [
	"5v ARGB Header 1"
];
export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

function CheckPacketLength(){
	let packet = [0x52];
	packet = device.get_report(packet, 200);

	return device.getLastReadSize();
}

function getZoneSlice(packet, zone, length = Zone_Length){
	return packet.slice(zone, zone + length);
}

function SetZoneColor(packet, zone, colors){
	//device.log(`Setting zone ${zone}`)
	//device.log(getZoneSlice(packet, zone))
	packet[zone + Zone_RED_Offset] = colors[0];
	packet[zone + Zone_RED_Offset1] = colors[0];
	packet[zone + Zone_GREEN_Offset] = colors[1];
	packet[zone + Zone_GREEN_Offset1] = colors[1];
	packet[zone + Zone_BLUE_Offset] = colors[2];
	packet[zone + Zone_BLUE_Offset1] = colors[2];
	//device.log(getZoneSlice(packet, zone))
}

function LogPacket(packet){
	device.log(`-----------------------------------`);
	device.log(getZoneSlice(packet, MSI_162_HEADER_OFFSET, 1));
	device.log(getZoneSlice(packet, MSI_162_JRGB1_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_RAINBOW1_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_CORSAIR_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_1_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_2_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_3_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_4_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_5_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_6_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_7_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_8_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_9_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_MAINBOARD_10_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_TEMP_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_JRGB2_OFFSET));
	device.log(getZoneSlice(packet, MSI_162_SAVE_FLAG, 1));
	device.log(`-----------------------------------`);

}
export function Initialize() {
	CheckPacketLength();
	CreateRGBHeaders();
	CreateARGBHeaders();
}

function CreateRGBHeaders(){
	for(let header = 0; header < 2;header++){
		//"Ch1 | Port 1"
		device.createSubdevice(HeaderArray[header]);
		// Parent Device + Sub device Name + Ports
		device.setSubdeviceName(HeaderArray[header], `${ParentDeviceName} - ${HeaderArray[header]}`);
		device.setSubdeviceSize(HeaderArray[header], 3, 3);
	}
}

function CreateARGBHeaders(){
	for(let header = 0; header < 1;header++){
		//"Ch1 | Port 1"
		device.createSubdevice(ARGBHeaderArray[header]);
		// Parent Device + Sub device Name + Ports
		device.setSubdeviceName(ARGBHeaderArray[header], `${ParentDeviceName} - ${ARGBHeaderArray[header]}`);
		device.setSubdeviceSize(ARGBHeaderArray[header], 3, 3);
	}
}

function SetMainboardLeds(overrideColor) {
	for(let iIdx = 0; iIdx < vLedMap.length; iIdx++) {
		const iPxX = vLedPositions[iIdx][0];
		const iPxY = vLedPositions[iIdx][1];
		var col;

		if(overrideColor){
			col = hexToRgb(overrideColor);
		}else if (LightingMode === "Forced") {
			col = hexToRgb(forcedColor);
		}else{
			col = device.color(iPxX, iPxY);
		}

		SetZoneColor(configPacket, mainboardDict[iIdx], col);
	}
}

function SetRGBHeaderLeds(overrideColor){

	for(let iIdx = 0; iIdx < HeaderArray.length; iIdx++) {
		var col;

		if(overrideColor){
			col = hexToRgb(overrideColor);
		}else if (LightingMode === "Forced") {
			col = hexToRgb(forcedColor);
		}else{
			col = device.subdeviceColor(HeaderArray[iIdx], 1, 1);
		}

		SetZoneColor(configPacket, HeaderDict[iIdx], col);
	}
}

function  SetARGBHeaderLeds(overrideColor){
	for(let iIdx = 0; iIdx < ARGBHeaderArray.length; iIdx++){
		var col;

		if(overrideColor){
			col = hexToRgb(overrideColor);
		}else if (LightingMode === "Forced") {
			col = hexToRgb(forcedColor);
		}else{
			col = device.subdeviceColor(ARGBHeaderArray[iIdx], 1, 1);
		}

		SetZoneColor(configPacket, ARGBHeaderDict[iIdx], col);
	}
}
var configPacket = initialPacket.slice();
export function Render() {
	if(CheckPacketLength() != 162){
		device.log("PACKET LENGTH ERROR. ABORTING RENDERING");

		return;
	}

	SetMainboardLeds();

	SetRGBHeaderLeds();

	SetARGBHeaderLeds();

	//LogPacket(configPacket)
	device.log(configPacket);
	device.send_report(configPacket, 162);

	device.pause(30);
}


export function Shutdown(SystemSuspending) {
	if(CheckPacketLength() != 162){
		device.log("PACKET LENGTH ERROR. ABORTING RENDERING");

		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;

	SetMainboardLeds(color);

	SetRGBHeaderLeds(color);

	SetARGBHeaderLeds(color);

	device.send_report(configPacket, 162);
}

export function Validate(endpoint) {
	return endpoint.interface === 2 || endpoint.interface === 0;
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/msi/motherboards/motherboard.png";
}
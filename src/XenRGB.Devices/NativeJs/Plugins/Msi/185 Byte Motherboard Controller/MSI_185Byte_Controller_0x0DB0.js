import {Assert} from "@SignalRGB/Errors.js";
import systeminfo from "@SignalRGB/systeminfo";
import permissions from "@SignalRGB/permissions";
export function Name() { return "MSI Mystic Light Controller"; }
export function VendorId() { return 0x0DB0; }
export function Documentation(){ return "troubleshooting/msi"; }
// DO NOT PID SWAP THIS IF YOU DONT KNOW WHAT YOUR DOING
export function ProductId() { return Object.keys(MSIMotherboard.Library);}
// YOU CAN BRICK THESE MOTHERBOARDS RGB CONTROLLER WITH ONE WRONG PACKET
export function Publisher() { return "WhirlwindFX"; }
export function Size() { return [1, 1]; }
export function Type() { return "Hid"; }
export function DefaultPosition(){return [0, 0];}
export function DefaultScale(){return 8.0;}
export function DeviceType(){return "motherboard";}
export function ImageUrl() { return "https://assets.signalrgb.com/devices/brands/msi/motherboards/motherboard.png"; }
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
	];
}
export function ConflictingProcesses() {
	return ["LedKeeper.exe", "Dragon Center", "DCv2.exe", "LightKeeperService.exe", "LightKeeperService2.exe" ];
}

const ParentDeviceName = "Mystic Light Controller";

const DeviceMaxLedLimit = 480;

//Channel Name, Led Limit
const ChannelArray = [];

let vLedNames = [];
let vLedPositions = [];

let perLED = false;
let CorsairHeaders = 0;
let ARGBHeaders = 0;
let OnboardLEDs = 0;
let JPipeLEDs = 0;
let RGBHeaders = 0;
let gen2Support = false;

let usesNewProtocol = false;

let isV3 = false;

let motherboardName;

/** @param {UpdatedPermissions} updatedPermissions */
function onPermissionsUpdated(updatedPermissions){
	console.log(updatedPermissions);

	if(updatedPermissions["lighting"] === true){
		initializeMotherboard();
	}
}

export function LedNames() {
	return vLedNames;
}

export function LedPositions() {
	return vLedPositions;
}

export function Initialize() {
	permissionManager.Register();
	checkProtocolVersion();
	fetchWmiInfo();

	if(!motherboardName) {
		device.log("Failed to grab motherboard name from WMI. Stopping render loop.", { toFile : true });

		return;
	}

	device.setName("MSI " + motherboardName);

	if(permissionManager.GetPermission("lighting")) {
		initializeMotherboard();
	}

	device.write([0x01, 0xbb, 0x00, 0x00, 0x00, 0x00, 0x01], 64); //Let's make sure users have their leds on in bios.
}

function initializeMotherboard() {

	if(!usesNewProtocol) {
		MSIMotherboard.checkPerLEDSupport();

		if(oldProto.getConfigExists()) {
			isV3 = true;
			oldProto.createZones();
			oldProto.disableGen2();
			oldProto.setHeaderConfigs();
			device.send_report(oldProto.standardPerledPacket, 185);
		} else {
			MSIMotherboard.detectGen2Support();
			MSIMotherboard.createLEDs();

			if(perLED) { MSIMotherboard.setPerledMode(true); }
		}

	} else {
		newProto.readDeviceZones();
		newProto.createZones();
		newProto.setDeviceZones();

		device.send_report(newProto.standardPerledPacket, 290);
	}

	checkEZLedOn();
}

export function Render() {
	if(!motherboardName) {
		return;
	}

	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	if(!usesNewProtocol) {
		if(!isV3) {
			MSIMotherboard.choosePacketSendType();
		} else {
			oldProto.sendColors();
		}
	} else {
		newProto.sendColors();
	}
}

export function Shutdown(SystemSuspending) {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;

	if(!usesNewProtocol) {
		if(!isV3) {
			MSIMotherboard.choosePacketSendType(color);
		} else {
			oldProto.sendColors(color);
		}
	} else {
		newProto.sendColors(color);
	}
}

function fetchWmiInfo() {
	let biosInfo;
	let motherboardInfo;
	let attempts = 0;

	while((!motherboardInfo || !biosInfo) && attempts < 10) {
		biosInfo = systeminfo.GetBiosInfo();
		motherboardInfo = systeminfo.GetMotherboardInfo();
		device.pause(500);
		attempts++;
	}

	if(!motherboardInfo) {
		device.notify("Failed to fetch motherboard name from WMI!", "Please reach out to official SignalRGB support for help.", 1);
		Assert.fail("Failed to fetch motherboard name from WMI after 10 attempts!");

		return;
	}

	motherboardName = motherboardInfo.model;

	if(!biosInfo) {
		device.notify("Failed to fetch bios version from WMI!", "Please reach out to official SignalRGB support for help.", 1);
		Assert.fail("Failed to fetch bios version from WMI after 10 attempts!");

		return;
	}

	compareBiosVersion(biosInfo, motherboardName);
}

function compareBiosVersion(biosInfo, motherboardName) {
	if(motherboardName in newProto.Library) {
		const workingBios = new Date("April 1, 2025");
		const currentBios = new Date(biosInfo.releaseDate);

		device.log(`Bios Release Date: ${currentBios}`);

		if(currentBios < workingBios) {
			device.notify("Bios is out of date!", "This may lead to degraded lighting performance.", 1);
		}
	}

}

function checkProtocolVersion() {
	const PerledLength = device.send_report([0x51], 761);

	if(PerledLength > 0) {
		device.log('Device uses new protocol!', { toFile : true});
		usesNewProtocol = true;
	}
}

function checkEZLedOn() {
	device.clearReadBuffer();

	const packet = [0x01, 0xBA];

	for (let index = 2; index < 64; index++) {
		packet[index] = 0xCC;
	}

	device.write(packet, 64);

	const ezLedEnabled = device.read(packet, 64)[6] === 1;
	device.log(`EZLED Enabled?: ${ezLedEnabled}`);

	if(!ezLedEnabled) {
		device.notify("EZ LED is Disabled in BIOS!", "Ensure that EZ Led is enabled in BIOS.", 1);
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
	return endpoint.interface === 0 || endpoint.interface === 2;
}

class MysticLight761 {
	constructor() {
		this.deviceConfig;

		this.Library = {
			// B840
			"B840 GAMING PLUS WIFI (MS-7E57)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B840-P WIFI (MS-7E57)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B840M GAMING WIFI6E (MS-7E76)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B840M GAMING PLUS WIFI6E (MS-7E77)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B840M-B (MS-7E76)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B840-VC WIFI (MS-7E57)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B840M-P WIFI6E (MS-7E77)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},

			// B850
			"B850 GAMING PLUS WIFI (MS-7E56)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850M GAMING PLUS WIFI (MS-7E66)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850 GAMING PLUS WIFI6E (MS-7E80)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850M GAMING PLUS WIFI6E (MS-7E81)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850M GAMING WIFI6E (MS-7E87)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850M GAMING PRO WIFI6E (MS-7E88)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850-P WIFI (MS-7E56)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850M-P (MS-7E71)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850M-P WIFI (MS-7E71)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850M-A WIFI (MS-7E66)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG B850 TOMAHAWK MAX WIFI (MS-7E62)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG B850 EDGE TI WIFI (MS-7E62)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 11,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG B850I EDGE TI WIFI (MS-7E79)":
			{
				ARGBHeaders    : 1,
				EZConnector	   : true,
				OnboardZones : {
				}
			},
			"MAG B850M MORTAR WIFI (MS-7E61)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850-VC WIFI (MS-7E56)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850-S WIFI6E (MS-7E80)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850 GAMING PRO WIFI6E (MS-7E89)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850 GAMING PLUS WIFI PZ (MS-7E75)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B850MPOWER (MS-7E83)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850M-VC WIFI6E (MS-7E71)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG B850 TOMAHAWK WIFI (MS-7E53)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850M-G (MS-7E82)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B850M-A WIFI PZ (MS-7E78)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG B850 GAMING PLUS MAX WIFI (MS-7E56)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},

			// B860
			"B860 GAMING PLUS WIFI (MS-7E41)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B860M-A WIFI (MS-7E42)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG B860 TOMAHAWK WIFI (MS-7E39)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					},
					"JRGB2" : {
						"Name" : "12V RGB Header 2",
						"LedCount" : 1
					}
				}
			},
			"PRO B860-VC WIFI (MS-7E41)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO B860-P WIFI (MS-7E41)":
			{
				ARGBHeaders    : 2,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},

			// Z790
			"MEG Z790 GODLIKE (MS-7D85)":
			{
				ARGBHeaders    : 3,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "Placeholder",
						"LedCount" : 40,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},

			// X870
			"MAG X870 TOMAHAWK WIFI (MS-7E51)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"X870 GAMING PLUS WIFI (MS-7E47)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO X870-P WIFI (MS-7E47)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},

			// X870E
			"MPG X870E CARBON WIFI":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
					"JPIPE3" : {
						"Name" : "Carbon M.2",
						"LedCount" : 6
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG X870E CARBON WIFI (MS-7E49)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
					"JPIPE3" : {
						"Name" : "Carbon M.2",
						"LedCount" : 6
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG X870E CARBON MAX WIFI (MS-7E49)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
					"JPIPE3" : {
						"Name" : "Carbon M.2",
						"LedCount" : 6
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG X870E EDGE TI WIFI (MS-7E59)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 11,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MEG X870E GODLIKE (MS-7E48)":
			{
				ARGBHeaders    : 1,
				EZConnector  : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "Placeholder",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
					"JPIPE2" : {
						"Name" : "Placeholder 2",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
					"JPIPE3" : {
						"Name" : "Placeholder 3",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
				}
			},
			"MEG X870E GODLIKE X EDITION (MS-7E48)":
			{
				ARGBHeaders    : 1,
				EZConnector  : true,
				OnboardZones : {
				}
			},
			"MAG X870E TOMAHAWK WIFI (MS-7E59)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG X870E TOMAHAWK MAX WIFI PZ (MS-7E84)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO X870E-P WIFI (MS-7E70)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"X870E GAMING PLUS WIFI (MS-7E70)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG X870I EDGE TI EVO WIFI (MS-7E50)":
			{
				ARGBHeaders    : 1,
				EZConnector	   : true,
				OnboardZones : {}
			},
			"MEG X870E ACE MAX (MS-7E85)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 10,
						"Orientation" : "Vertical"
					},
					"JPIPE3" : {
						"Name" : "Carbon M.2",
						"LedCount" : 6
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG X870E GAMING PLUS MAX WIFI (MS-7E70)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},

			// Z890
			"MPG Z890 CARBON WIFI (MS-7E17)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 11,
						"Orientation" : "Vertical"
					},
					"JPIPE3" : {
						"Name" : "Carbon M.2",
						"LedCount" : 6
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG Z890 TOMAHAWK WIFI (MS-7E32)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO Z890-A WIFI (MS-7E32)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"Z890 GAMING PLUS WIFI (MS-7E34)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO Z890-S WIFI (MS-7E54)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO Z890-S WIFI WHITE (MS-7E54)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MEG Z890 ACE (MS-7E22)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO Z890-P (MS-7E34)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO Z890-P WIFI (MS-7E34)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"PRO Z890-S WIFI PZ (MS-7E58)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MEG Z890 UNIFY-X (MS-7E20)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 11,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MPG Z890 EDGE TI WIFI (MS-7E19)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 11,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"Z890 GAMING PLUS WIFI6E (MS-7E34)":
			{
				ARGBHeaders    : 3,
				EZConnector	   : true,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
		};

		this.modeDict = [
			"Off",
			"Wave",
			"Steady",
			"Flame",
			"Breathing",
			"ColorRing",
			"Lightning",
			"Recreation",
			"Meteor",
			"Advanced",
			"GodLike"
		];

		this.zoneDict = [
			"JARGB1",
			"JARGB2",
			"JARGB3",
			"JAF",
			"JPIPE1",
			"JPIPE2",
			"JPIPE3",
			"JPIPE4",
			"JPIPE5",
			"JRGB1",
			"JRGB2",
			"Onboard1",
			"Onboard2",
			"Onboard3",
			"Onboard4",
			"Onboard5",
			"Onboard6",
			"SelectAll"
		];

		this.addressDict = {
			"JRGB1" : 0x200,
			"JRGB2" : 0x201,
			"JPIPE1" : 0x300,
			"JPIPE2" : 0x301,
			"JPIPE3" : 0x302,
			"JPIPE4" : 0x303,
			"JPIPE5" : 0x304,
			"JARGB1" : 0x400,
			"JARGB2" : 0x401,
			"JARGB3" : 0x402,
			"SelectAll" : 0x600,
			"OnBoard1" : 0x700,
			"OnBoard2" : 0x701,
			"OnBoard3" : 0x702,
			"OnBoard4" : 0x703,
			"OnBoard5" : 0x704,
			"OnBoard6" : 0x705,
			"JAF" : 0x800,
		};

		this.standardPerledPacket = [
			0x50,
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x15, 0x78, //JARGB 1
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x15, 0x78, //JARGB 2
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x15, 0x78, //JARGB 3
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x15, 0x78, //JAF //Fans go here with the weird connector
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x95, 0x1e, //JPIPE1
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //JPIPE2 //95 for active zones 94 for inactive
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x95, 0x1e, //JPIPE3
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //JPIPE4
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //JPIPE5
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x95, 0x1e, //JRGB1
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //JRGB2
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //Onboard1
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //Onboard2
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //Onboard3
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //Onboard4
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //Onboard5
			0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x94, 0x1e, //Onboard6
			0x09, 0xff, 0x00, 0x00, 0x00, 0xff, 0x00, 0x00, 0x00, 0xff, 0xff, 0xff, 0xff, 0x03, 0x95, 0x1e, //Select all?
			0x00,
		];
	} //TODO: THIS HAS TO BE SET RIGHT PER BOARD MOST LIKELY

	getDeviceConfig() { return this.deviceConfig; }

	setDeviceConfig(deviceConfig) { this.deviceConfig = deviceConfig; }

	setZoneLeds(zone, ledNames, ledPositions) {
		this.deviceConfig.OnboardZones[zone].ledNames = ledNames;
		this.deviceConfig.OnboardZones[zone].ledPositions = ledPositions;
	}

	readDeviceZones() {

		const deviceZones = device.get_report([0x50], 290);
		device.log('Detecting Zones!', {toFile : true});

		//This in theory can be used for detection, but it doesn't give led counts so doesn't matter too much
		//However it should be plenty useful for adding new stuffs as it notes how many zones we have.
		for(let zones = 0; zones < 18; zones++) {
			const offset = (zones * 16) + 1;
			const mode = deviceZones[offset];

			if(mode !== 0) {
				const colorCount = (deviceZones[offset + 13] & 3);
				const mergeWithOnboard = (deviceZones[offset + 14] >> 7 & 1) === 1;
				const direction = (deviceZones[offset + 14] >> 6 & 1) === 0 ? "Outward" : "Inward"; //0 is out, 1 is in
				const colorSelection = (deviceZones[offset + 14] >> 5 & 1) === 0 ? "Rainbow" : "User"; //Rainbow or user
				const brightness = (deviceZones[offset + 14] >> 2 & 7) * 20; //20 incr from 0
				const speed = (deviceZones[offset + 14] & 3);// Low, Med, High
				const ledCount = deviceZones[offset + 15];

				device.log(`Zone ${this.zoneDict[zones]} Mode: ${this.modeDict[mode]} with ${ledCount} LEDs. Merged with Onboard: ${mergeWithOnboard}`, { toFile : true});

				device.log(`Zone ${this.zoneDict[zones]} Optional flags: ColCount: ${colorCount} Dir: ${direction} ColType: ${colorSelection} Bright: ${brightness} Speed: ${speed}`, { toFile : true});
			}
		}
	}
	/*eslint-disable complexity*/
	setDeviceZones() {
		for(let zones = 0; zones < 17; zones++) {
			const enabled = this.getDeviceConfig().OnboardZones[this.zoneDict[zones]] ? true : false;

			if(zones <= 3) {
				//These all seem to always be enabled?
				//this.standardPerledPacket[(zones * 16) + 15] = enabled? 0x95 : 0x94;
			} else {
				this.standardPerledPacket[(zones * 16) + 1] = enabled? 0x09 : 0x00;
				this.standardPerledPacket[(zones * 16) + 2] = enabled? 0xff : 0x00;
				this.standardPerledPacket[(zones * 16) + 6] = enabled? 0xff : 0x00;
				this.standardPerledPacket[(zones * 16) + 10] = enabled? 0xff : 0x00;
				this.standardPerledPacket[(zones * 16) + 11] = enabled? 0xff : 0x00;
				this.standardPerledPacket[(zones * 16) + 12] = enabled? 0xff : 0x00;
				this.standardPerledPacket[(zones * 16) + 13] = enabled? 0xff : 0x00;
				this.standardPerledPacket[(zones * 16) + 14] = enabled? 0x03 : 0x00;
				this.standardPerledPacket[(zones * 16) + 15] = enabled? 0x95 : 0x00;
			}
		}
	}

	generateLeds(count, orientation = 'Horizontal') {
		const ledInfo = {
			ledNames : [],
			ledPositions : [],
			size : orientation === 'Horizontal' ? [count, 1] : [1, count]
		};

		for(let iIdx = 0; iIdx < count; iIdx++) {
			ledInfo.ledNames.push(`LED ${iIdx + 1}`);
			ledInfo.ledPositions.push(orientation === 'Horizontal' ? [iIdx, 0] : [0, iIdx]);
		}

		return ledInfo;
	}

	createZones() {
		if(motherboardName in this.Library) {
			this.setDeviceConfig(this.Library[motherboardName]);

			this.createStandardLEDs(this.getDeviceConfig());
			device.log("Using Configuration From Table", {toFile:true});
		} else {
			device.notify("Unmapped Board Layout!", "Please reach out in the SignalRGB discord to get this motherboard supported.", 1);
			Assert.fail("Unmapped MSI 761 Board! : " + motherboardName);
		}

	}

	createStandardLEDs(deviceConfig) {

		for(const onboardZone in deviceConfig.OnboardZones) {
			this.createSubdevice(onboardZone);
		}

		this.SetupChannels();
	}

	SetupChannels() {
		device.SetLedLimit(DeviceMaxLedLimit);

		for(let i = 0; i < this.getDeviceConfig().ARGBHeaders; i++) {
			ChannelArray.push([`ARGB Header ${i + 1}`, 120]);
		}

		if(this.getDeviceConfig().EZConnector) {
			ChannelArray.push([`EZ Connector 1`, 120]);
		}

		for(let i = 0; i < ChannelArray.length; i++) {
			device.addChannel(ChannelArray[i][0], ChannelArray[i][1]);
		}
	}

	createSubdevice(subdevice) {
		//Have to do this because we're passing in key.
		const subDeviceData = this.getDeviceConfig().OnboardZones[subdevice];

		const ledInfo = this.generateLeds(subDeviceData.LedCount, subDeviceData?.Orientation ?? "Horizontal");

		device.createSubdevice(subDeviceData.Name);
		device.setSubdeviceName(subDeviceData.Name, `${ParentDeviceName} - ${subDeviceData.Name}`);
		device.setSubdeviceLeds(subDeviceData.Name, ledInfo.ledNames, ledInfo.ledPositions);
		device.setSubdeviceSize(subDeviceData.Name, ledInfo.size[0], ledInfo.size[1]);

		this.setZoneLeds(subdevice, ledInfo.ledNames, ledInfo.ledPositions);
	}

	GrabOnboardZone(zone, overrideColor) {
		const RGBData = [];

		const vLedPositions = zone.ledPositions;

		//TODO: Assert if positions aren't valid

		for(let iIdx = 0; iIdx < vLedPositions.length; iIdx++) {
			const iPxX = vLedPositions[iIdx][0];
			const iPxY = vLedPositions[iIdx][1];
			let col;

			if(overrideColor) {
				col = hexToRgb(overrideColor);
			} else if (LightingMode === "Forced") {
				col = hexToRgb(forcedColor);
			} else {
				col = device.subdeviceColor(zone.Name, iPxX, iPxY);
			}

			RGBData[iIdx * 3] = col[0];
			RGBData[iIdx * 3 + 1] = col[1];
			RGBData[iIdx * 3 + 2] = col[2];
		}

		return RGBData;
	}

	grabChannelRGBData(Channel, overrideColor) {
    	let ChannelLedCount = device.channel(ChannelArray[Channel][0]).LedCount();
		const componentChannel = device.channel(ChannelArray[Channel][0]);

		let RGBData = [];

		if(overrideColor){
			RGBData = device.createColorArray(overrideColor, ChannelLedCount, "Inline");
		} else if(LightingMode === "Forced") {
			RGBData = device.createColorArray(forcedColor, ChannelLedCount, "Inline");
		} else if(componentChannel.shouldPulseColors()) {
			ChannelLedCount = 80;

			const pulseColor = device.getChannelPulseColor(ChannelArray[Channel][0]);
			RGBData = device.createColorArray(pulseColor, ChannelLedCount, "Inline");
		} else {
			RGBData = device.channel(ChannelArray[Channel][0]).getColors("Inline");
		}

    	return RGBData;
	}

	sendColors(overrideColor) {
		if(this.getDeviceConfig() === undefined) {
			//Silent failure because otherwise all of my diagnostic info is impossible to find.
			//We'll reach an assert before this so users can still see that there's an issue.
			return;
		}

		const ARGBHeaders = this.getDeviceConfig().ARGBHeaders;
		const hasEZHeader = this.getDeviceConfig().EZConnector;
		let OnboardLEDData = [];

		for(const onboardZone in this.getDeviceConfig().OnboardZones) {
			OnboardLEDData = OnboardLEDData.concat(this.GrabOnboardZone(this.getDeviceConfig().OnboardZones[onboardZone], overrideColor));
		}

		for(let headers = 0; headers < ARGBHeaders; headers++) {
			const headerData = this.grabChannelRGBData(headers, overrideColor);

			if(headerData.length > 0) {
				device.send_report([0x51, 0x09, 0x04, headers, 0x00, 0x00, (headerData.length / 3)].concat(headerData), 727);
				device.get_report([0x51], 727);
			}
		}

		if(hasEZHeader) {
			const EZHeaderData = this.grabChannelRGBData(ARGBHeaders, overrideColor);

			if(EZHeaderData.length > 0) {
				device.send_report([0x51, 0x09, 0x08, 0x00, 0x00, 0x00, (EZHeaderData.length / 3)].concat(EZHeaderData), 727);
				device.get_report([0x51], 727);
			}
		}

		if(OnboardLEDData.length > 0) {
			device.send_report([0x51, 0x09, 0x06, 0x00, 0x00, 0x00, (OnboardLEDData.length / 3)].concat(OnboardLEDData), 727);
			device.get_report([0x51], 727);
		}
	}
}

const newProto = new MysticLight761();

class MysticLight185V3 {
	constructor() {
		this.deviceConfig;

		this.Library = {
			// B650
			"B650 GAMING PLUS WIFI (MS-7E26)":
			{
				ARGBHeaders    : 2,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					},
					"JRGB2" : {
						"Name" : "12v RGB Header 2",
						"LedCount" : 1
					}
				}
			},

			// X670E
			"X670E GAMING PLUS WIFI (MS-7E16)":
			{
				ARGBHeaders    : 2,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					},
					"JRGB2" : {
						"Name" : "12v RGB Header 2",
						"LedCount" : 1
					}
				}
			},
		};

		this.modeDict = [
			"Disable",
			"NoAnimation",
			"Breathing",
			"Flashing",
			"DoubleFlashing",
			"Lighting",
			"MSIMarquee",
			"Meteor",
			"WaterDrop",
			"MSIRainbow",
			"POP",
			"RAP",
			"JAZZ",
			"Play",
			"Movie",
			"ColorRing",
			"Planetary",
			"DoubleMeteor",
			"Energy",
			"Blink",
			"Clock",
			"ColorPulse",
			"ColorShift",
			"ColorWave",
			"Marquee",
			"Rainbow",
			"RainbowWave",
			"Visor",
			"JRainbow",
			"RainbowFlahing",
			"RainbowDoubleFlashing",
			"Random",
			"FANControl",
			"Disable2",
			"ColorRingFlashing",
			"ColorRingDoubleFlashing",
			"Stack",
			"CorsairiQUE",
			"Fire",
			"Lava",
			"MixEffect",
			"OnBoard",
			"End"
		];

		this.zoneDict = [
			"JRGB1",
			"JPIPE1",
			"JPIPE2",
			"JRAINBOW1",
			"JRAINBOW2",
			"JCORSAIR",
			"JCORSAIR_OuterLL120",
			"OnboardLED",
			"OnBoardLED1",
			"OnBoardLED2",
			"OnBoardLED3",
			"OnBoardLED4",
			"OnBoardLED5",
			"OnBoardLED6",
			"OnBoardLED7",
			"OnBoardLED8",
			"OnBoardLED9",
			"JRGB2",
			"OnBoardLED10"
		];

		this.addressDict = {
			"JRGB1" : 0x01,
			"JPIPE1" : 0x0B,
			"JPIPE2" : 0x15,
			"JRAINBOW1" : 0x1F,
			"JRAINBOW2" : 0x2A,
			"JCORSAIR" : 0x35,
			"JCORSAIR_OuterLL120" : 0x40,
			"OnboardLED" : 0x4A,
			"OnBoardLED1" : 0x54,
			"OnBoardLED2" : 0x5E,
			"OnBoardLED3" : 0x68,
			"OnBoardLED4" : 0x72,
			"OnBoardLED5" : 0x7C,
			"OnBoardLED6" : 0x86,
			"OnBoardLED7" : 0x90,
			"OnBoardLED8" : 0x9A,
			"OnBoardLED9" : 0xA4,
			"JRGB2" : 0xAE,
			"OnBoardLED10" : 0xAE
		};

		this.standardPerledPacket = [
			0x52, //enable, r,g,b, options, r,g,b,sync,seperator
			0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow1
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow2
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow3 or Corsair?
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JCorsair other?
			0x25, 0x00, 0x00, 0x00, 0xa9, 0x00, 0x00, 0x00, 0xb1, 0x00, //JOnboard1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard2
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard3
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard4
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard5
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard6
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard7
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard8
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard9
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard10
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
			0x00 //Save Flag
		];
	}

	getDeviceConfig() { return this.deviceConfig; }

	setDeviceConfig(deviceConfig) { this.deviceConfig = deviceConfig; }

	getConfigExists() { return motherboardName in this.Library; }

	setHeaderConfigs() {
		//TODO: Figure out what flags are needed for header 1 and 2. Header 3 is goofy because of Corsair header remnants
		const headers = this.getDeviceConfig().ARGBHeaders;
		this.standardPerledPacket[31] = headers > 0 ? 0x25 : 0x01;
		this.standardPerledPacket[42] = headers > 1 ? 0x25 : 0x01;
		this.standardPerledPacket[53] = headers > 2 ? 0x25 : 0x01;
	}

	setZoneLeds(zone, ledNames, ledPositions) {
		this.deviceConfig.OnboardZones[zone].ledNames = ledNames;
		this.deviceConfig.OnboardZones[zone].ledPositions = ledPositions;
	}

	disableGen2() {
		device.log("Gen 2 Supported.");

		for(let headers = 0; headers < this.getDeviceConfig().ARGBHeaders; headers++){
			device.write([0x01, 0x84, 0x00, 0x00, 0x00, 0x00, headers, 0x00], 64);
			device.pause(1000);
		}
	}

	generateLeds(count, orientation = 'Horizontal') {
		const ledInfo = {
			ledNames : [],
			ledPositions : [],
			size : orientation === 'Horizontal' ? [count, 1] : [1, count]
		};

		for(let iIdx = 0; iIdx < count; iIdx++) {
			ledInfo.ledNames.push(`LED ${iIdx + 1}`);
			ledInfo.ledPositions.push(orientation === 'Horizontal' ? [iIdx, 0] : [0, iIdx]);
		}

		return ledInfo;
	}

	createZones() {
		if(motherboardName in this.Library) {
			this.setDeviceConfig(this.Library[motherboardName]);

			this.createStandardLEDs(this.getDeviceConfig());
			device.log("Using Configuration From Table", {toFile:true});
		}
	}

	createStandardLEDs(deviceConfig) {
		for(const onboardZone in deviceConfig.OnboardZones) {
			this.createSubdevice(onboardZone);
		}

		this.SetupChannels();
	}

	SetupChannels() {
		device.SetLedLimit(DeviceMaxLedLimit);

		for(let i = 0; i < this.getDeviceConfig().ARGBHeaders; i++) {
			ChannelArray.push([`Jrainbow ${i + 1}`, 120]);
		}

		for(let i = 0; i < ChannelArray.length; i++) {
			device.addChannel(ChannelArray[i][0], ChannelArray[i][1]);
		}
	}

	createSubdevice(subdevice) {
		//Have to do this because we're passing in key.
		const subDeviceData = this.getDeviceConfig().OnboardZones[subdevice];

		const ledInfo = this.generateLeds(subDeviceData.LedCount, subDeviceData?.Orientation ?? "Horizontal");

		device.createSubdevice(subDeviceData.Name);
		device.setSubdeviceName(subDeviceData.Name, `${ParentDeviceName} - ${subDeviceData.Name}`);
		device.setSubdeviceLeds(subDeviceData.Name, ledInfo.ledNames, ledInfo.ledPositions);
		device.setSubdeviceSize(subDeviceData.Name, ledInfo.size[0], ledInfo.size[1]);

		this.setZoneLeds(subdevice, ledInfo.ledNames, ledInfo.ledPositions);
	}

	GrabOnboardZone(zone, overrideColor) {
		const RGBData = [];

		const vLedPositions = zone.ledPositions;

		//TODO: Assert if positions aren't valid

		for(let iIdx = 0; iIdx < vLedPositions.length; iIdx++) {
			const iPxX = vLedPositions[iIdx][0];
			const iPxY = vLedPositions[iIdx][1];
			let col;

			if(overrideColor) {
				col = hexToRgb(overrideColor);
			} else if (LightingMode === "Forced") {
				col = hexToRgb(forcedColor);
			} else {
				col = device.subdeviceColor(zone.Name, iPxX, iPxY);
			}

			RGBData[iIdx * 3] = col[0];
			RGBData[iIdx * 3 + 1] = col[1];
			RGBData[iIdx * 3 + 2] = col[2];
		}

		return RGBData;
	}

	grabChannelRGBData(Channel, overrideColor) {
    	let ChannelLedCount = device.channel(ChannelArray[Channel][0]).LedCount();
		const componentChannel = device.channel(ChannelArray[Channel][0]);

		let RGBData = [];

		if(overrideColor){
			RGBData = device.createColorArray(overrideColor, ChannelLedCount, "Inline");
		} else if(LightingMode === "Forced") {
			RGBData = device.createColorArray(forcedColor, ChannelLedCount, "Inline");
		} else if(componentChannel.shouldPulseColors()) {
			ChannelLedCount = 80;

			const pulseColor = device.getChannelPulseColor(ChannelArray[Channel][0]);
			RGBData = device.createColorArray(pulseColor, ChannelLedCount, "Inline");
		} else {
			RGBData = device.channel(ChannelArray[Channel][0]).getColors("Inline");
		}

    	return RGBData;
	}

	sendColors(overrideColor) {
		if(this.getDeviceConfig() === undefined) {
			//Silent failure because otherwise all of my diagnostic info is impossible to find.
			//We'll reach an assert before this so users can still see that there's an issue.
			return;
		}

		const ARGBHeaders = this.getDeviceConfig().ARGBHeaders;
		let OnboardLEDData = [];

		for(const onboardZone in this.getDeviceConfig().OnboardZones) {
			OnboardLEDData = OnboardLEDData.concat(this.GrabOnboardZone(this.getDeviceConfig().OnboardZones[onboardZone], overrideColor));
		}

		for(let headers = 0; headers < ARGBHeaders; headers++) {
			const headerData = this.grabChannelRGBData(headers, overrideColor);

			if(headerData.length > 0) {
				device.send_report([0x53, 0x25, 0x04, headers, 0x00].concat(headerData), 525);
				device.pause(1);
			}
		}

		if(OnboardLEDData.length > 0) {
			device.send_report([0x53, 0x25, 0x06, 0x00, 0x00].concat(OnboardLEDData), 725);
			device.pause(1);
		}
	}
}

const oldProto = new MysticLight185V3();

class MysticLight {
	constructor() {

		this.ConfigurationOverrides =
		{
			"MPG Z790 EDGE TI MAX WIFI (MS-7E25)":
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			"MAG Z790 TOMAHAWK MAX WIFI (MS-7E25)":
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
		};

		this.Library =
		{
			0x0076 : //X670E Tomahawk Headers? // I doubt this is actually the pid or MSI god so help me.
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x0ABE : //X670E Godlike M.2 RGB zone
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 0,
				ARGBHeaders    : 0,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 10,
			},
			0xE777 : //Z790 Godlike M.2 RGB zone ?
			{
				OnboardLEDs    : 4,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : true,
				JARGB_V2		  : true,
			},
		};

		this.OffsetDict =
		{
			MSI_185_JRGB1_OFFSET 	      : 0x01,
			MSI_185_JPIPE1_OFFSET         : 0x0B,
			MSI_185_JPIPE2_OFFSET         : 0x15,
			MSI_185_RAINBOW1_OFFSET   	  : 0x1F,
			MSI_185_RAINBOW2_OFFSET   	  : 0x2A,
			MSI_185_CORSAIR_OFFSET        : 0x35,
			MSI_185_CORSAIR_BACKUP_OFFSET : 0x40,
			MSI_185_MAINBOARD_1_OFFSET    : 0x4A,
			MSI_185_MAINBOARD_2_OFFSET    : 0x54,
			MSI_185_MAINBOARD_3_OFFSET    : 0x5E,
			MSI_185_MAINBOARD_4_OFFSET 	  : 0x68,
			MSI_185_MAINBOARD_5_OFFSET    : 0x72,
			MSI_185_MAINBOARD_6_OFFSET    : 0x7C,
			MSI_185_MAINBOARD_7_OFFSET    : 0x86,
			MSI_185_MAINBOARD_8_OFFSET    : 0x90,
			MSI_185_MAINBOARD_9_OFFSET    : 0x9A,
			MSI_185_MAINBOARD_10_OFFSET   : 0xA4,
			MSI_185_JRGB2_OFFSET 		  : 0xAE
		};

		this.packetOffsets =
		{
			mainboardDict :
			[
				this.OffsetDict.MSI_185_MAINBOARD_1_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_2_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_3_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_4_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_5_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_6_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_7_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_8_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_9_OFFSET,
				this.OffsetDict.MSI_185_MAINBOARD_10_OFFSET
			],

			JPipeDict :
			[
				this.OffsetDict.MSI_185_JPIPE1_OFFSET,
				this.OffsetDict.MSI_185_JPIPE2_OFFSET
			],

			RGBHeaderDict :
			[
				this.OffsetDict.MSI_185_JRGB1_OFFSET,
				this.OffsetDict.MSI_185_JRGB2_OFFSET
			],

			ARGBHeaderDict :
			[
				this.OffsetDict.MSI_185_RAINBOW1_OFFSET,
				this.OffsetDict.MSI_185_RAINBOW2_OFFSET
			],

			CorsairDict :
			[
				this.OffsetDict.MSI_185_CORSAIR_OFFSET,
				this.OffsetDict.MSI_185_CORSAIR_BACKUP_OFFSET
			]
		};

		this.ComponentArrays =
		{
			JRainbowArray :
			[
				["JRainbow 1", 120],
				["JRainbow 2", 120],
				["Jrainbow 3", 120]
			],

			JCorsairArray :
			[
				["JCorsair",  120],
			]
		};

		this.LEDArrays =
		{
			OnboardArray :
			[
				"Mainboard Led 1",
				"Mainboard Led 2",
				"Mainboard Led 3",
				"Mainboard Led 4",
				"Mainboard Led 5",
				"Mainboard Led 6",
				"Mainboard Led 7",
				"Mainboard Led 8",
				"Mainboard Led 9",
				"Mainboard Led 10",
				"Mainboard Led 11",
				"Mainboard Led 12",
				"Mainboard Led 13",
				"Mainboard Led 14",
				"Mainboard Led 15",
				"Mainboard Led 16",
				"Mainboard Led 17",
				"Mainboard Led 18",
				"Mainboard Led 19",
				"Mainboard Led 20"
			],

			JPipeArray :
			[
				"Jpipe Led 1",
				"Jpipe Led 2",
			],

			RGBHeaderArray :
			[
				"12v RGB Header 1",
				"12v RGB Header 2"
			],

			CorsairHeaderArray :
			[
				"Corsair",
				"Corsair Outer"
			],

			ARGBHeaderArray :
			[
				"5v ARGB Header 1",
				"5v ARGB Header 2"
			]
		};

		this.initialPacket =
		[
			0x52, //Header
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00, //JRGB1
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00,
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x64,
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x64,
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x82, 0x54, 0x0A,
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x80, 0x00,
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
			0x01, 0xFF, 0x00, 0x00, 0x28, 0xFF, 0x00, 0x00, 0x00, 0x00, //JRGB2
			0x00 //No saving
		];

		this.perledpacket =
		[
			0x52, //enable, r,g,b, options, r,g,b,sync,seperator
			0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
			0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, 0x4B, //JRainbow1 //Extra Byte determines number of leds
			0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, 0x4B, //JRainbow2
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x82, 0x00, 0x78, //JRainbow3 or Corsair?
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JCorsair other?
			0x25, 0x00, 0x00, 0x00, 0xa9, 0x00, 0x00, 0x00, 0x9f, 0x00, //JOnboard1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard2
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard3
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard4
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard5
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard6
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard7
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard8
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard9
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard10
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
			0x00 //Save Flag
		];

		this.splitPerLEDPacket =
		[
			0x52, //enable, r,g,b, options, r,g,b,sync,seperator
			0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow1 //Extra Byte determines number of leds We're keeping these capped at 75 for now. No boom. This does give headroom for up to 200.
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow2
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x82, 0x00, 0x78, //JRainbow3 or Corsair?
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JCorsair other?
			0x25, 0x00, 0x00, 0x00, 0xa9, 0x00, 0x00, 0x00, 0xb1, 0x00, //JOnboard1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard2
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard3
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard4
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard5
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard6
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard7
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard8
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard9
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard10
			0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
			0x00 //Save Flag
		];

		this.lastonboardData = [];
		this.lastheader1Data = [];
		this.lastheader2Data = [];
		this.lastheader3Data = [];

		this.header1LEDCount = 0;
		this.header2LEDCount = 0;
		this.header3LEDCount = 0;

	}

	checkPerLEDSupport() {
		const response = device.send_report([0x53], 725);

		if(response > 0) {
			perLED = true;
			device.log("Motherboard is PerLED 🙂", {toFile:true});
		} else {
			device.log("Motherboard is not PerLED 😔", {toFile:true});
		}

		if(this.Library[device.productId()]["ForceZoneBased"] === true) //I'm leaving this untouched, as no new boards are zone based. Lord so help me if that changes MSI.
		{
			perLED = false;
		}
	}

	CheckPacketLength() {
		device.get_report([0x52], 200);

		return device.getLastReadSize();
	}

	createLEDs() {
		if(motherboardName in this.ConfigurationOverrides) {
			this.createStandardLEDs(this.ConfigurationOverrides[motherboardName]);
			device.log("Using Configuration Override", {toFile:true});
		} else {
			console.log("Creating device properties: " + device.productId());
			this.createStandardLEDs(this.Library[device.productId()]);
		}

		device.log(`Device has ${OnboardLEDs} Onboard LEDs, ${RGBHeaders} RGB Headers, ${ARGBHeaders} ARGB Headers, and ${JPipeLEDs} JPipe LEDs.`);
	}

	createStandardLEDs(configTable) {

		for(let RGBHeaders = 0; RGBHeaders < configTable["RGBHeaders"]; RGBHeaders++) {
			this.createSubdevice(this.LEDArrays.RGBHeaderArray[RGBHeaders]);
		}

		ARGBHeaders = configTable["ARGBHeaders"];
		CorsairHeaders = configTable["CorsairHeaders"];

		if(perLED === false) {
			for(let JPipeLEDs = 0; JPipeLEDs < configTable["JPipeLEDs"]; JPipeLEDs++) {
				this.createSubdevice(this.LEDArrays.JPipeArray[JPipeLEDs]);
			}

			for(let ARGBHeaders = 0; ARGBHeaders < configTable["ARGBHeaders"]; ARGBHeaders++) {
				this.createSubdevice(this.LEDArrays.ARGBHeaderArray[ARGBHeaders]);
			}

			for(let CorsairHeaders = 0; CorsairHeaders < configTable["CorsairHeaders"]; CorsairHeaders++) {
				this.createSubdevice(this.LEDArrays.CorsairHeaderArray[CorsairHeaders]);
			}

			for(let OnboardLEDs = 0; OnboardLEDs < configTable["OnboardLEDs"]; OnboardLEDs++) {
				this.createSubdevice(this.LEDArrays.OnboardArray[OnboardLEDs]);
			}

			JPipeLEDs = configTable["JPipeLEDs"];
			OnboardLEDs = configTable["OnboardLEDs"];
		} else {

			OnboardLEDs = configTable["PerLEDOnboardLEDs"];

			vLedNames = [];
			vLedPositions = [];

			for(let deviceLEDs = 0; deviceLEDs < OnboardLEDs; deviceLEDs++) {
				device.removeSubdevice(this.LEDArrays.OnboardArray[deviceLEDs]);
				vLedNames.push(`LED ${deviceLEDs + 1}`);
				vLedPositions.push([ deviceLEDs, 0 ]);
				device.setSize([vLedPositions.length+1, 2]);
				device.setControllableLeds(vLedNames, vLedPositions);
			}

			this.SetupChannels();
		}

		RGBHeaders = configTable["RGBHeaders"];
	}

	createConfigurationOverrideLEDs() {
		for(let RGBHeaders = 0; RGBHeaders < this.ConfigurationOverrides[motherboardName]["RGBHeaders"]; RGBHeaders++) {
			this.createSubdevice(this.LEDArrays.RGBHeaderArray[RGBHeaders]);
		}

		ARGBHeaders = this.ConfigurationOverrides[motherboardName]["ARGBHeaders"];
		CorsairHeaders = this.ConfigurationOverrides[motherboardName]["CorsairHeaders"];

		if(perLED === false) {
			for(let JPipeLEDs = 0; JPipeLEDs < this.ConfigurationOverrides[motherboardName]["JPipeLEDs"]; JPipeLEDs++) {
				this.createSubdevice(this.LEDArrays.JPipeArray[JPipeLEDs]);
			}

			for(let ARGBHeaders = 0; ARGBHeaders < this.ConfigurationOverrides[motherboardName]["ARGBHeaders"]; ARGBHeaders++) {
				this.createSubdevice(this.LEDArrays.ARGBHeaderArray[ARGBHeaders]);
			}

			for(let CorsairHeaders = 0; CorsairHeaders < this.ConfigurationOverrides[motherboardName]["CorsairHeaders"]; CorsairHeaders++) {
				this.createSubdevice(this.LEDArrays.CorsairHeaderArray[CorsairHeaders]);
			}

			for(let OnboardLEDs = 0; OnboardLEDs < this.ConfigurationOverrides[motherboardName]["OnboardLEDs"]; OnboardLEDs++) {
				this.createSubdevice(this.LEDArrays.OnboardArray[OnboardLEDs]);
			}

			JPipeLEDs = this.ConfigurationOverrides[motherboardName]["JPipeLEDs"];
			OnboardLEDs = this.ConfigurationOverrides[motherboardName]["OnboardLEDs"];
		} else {

			const moboName = motherboardName;

			OnboardLEDs = this.ConfigurationOverrides[moboName]["PerLEDOnboardLEDs"];

			vLedNames = [];
			vLedPositions = [];

			for(let deviceLEDs = 0; deviceLEDs < OnboardLEDs; deviceLEDs++) {
				device.removeSubdevice(this.ConfigurationOverrides[moboName]["PerLEDOnboardLEDs"]);
				vLedNames.push(`LED ${deviceLEDs + 1}`);
				vLedPositions.push([ deviceLEDs, 0 ]);
				device.setSize([vLedPositions.length+1, 2]);
				device.setControllableLeds(vLedNames, vLedPositions);
			}

			this.SetupChannels();
		}

		RGBHeaders  = this.ConfigurationOverrides[motherboardName]["RGBHeaders"];
	}

	SetupChannels() {
		device.SetLedLimit(DeviceMaxLedLimit);

		for(let i = 0; i < ARGBHeaders; i++) {
			ChannelArray.push(this.ComponentArrays.JRainbowArray[i]);
		}

		for(let i = 0; i < CorsairHeaders; i++) {
			ChannelArray.push(this.ComponentArrays.JCorsairArray[i]);
		}

		for(let i = 0; i < ChannelArray.length; i++) {
			device.addChannel(ChannelArray[i][0], ChannelArray[i][1]);
		}
	}

	createSubdevice(SubdeviceName) {

		device.createSubdevice(SubdeviceName);
		device.setSubdeviceName(SubdeviceName, `${ParentDeviceName} - ${SubdeviceName}`);
		device.setSubdeviceSize(SubdeviceName, 3, 3);
	}

	setDeviceZones(overrideColor) {
		this.setZoneLeds(this.packetOffsets.mainboardDict, this.LEDArrays.OnboardArray, OnboardLEDs, overrideColor);
		this.setZoneLeds(this.packetOffsets.JPipeDict, this.LEDArrays.JPipeArray, JPipeLEDs, overrideColor);
		this.setZoneLeds(this.packetOffsets.ARGBHeaderDict, this.LEDArrays.ARGBHeaderArray, ARGBHeaders, overrideColor);
		this.setZoneLeds(this.packetOffsets.CorsairDict, this.LEDArrays.CorsairHeaderArray, CorsairHeaders, overrideColor);
		this.setZoneLeds(this.packetOffsets.RGBHeaderDict, this.LEDArrays.RGBHeaderArray, RGBHeaders, overrideColor);
	}

	setZoneLeds(zone, zoneArray, zoneLEDs, overrideColor) {
		for(let iIdx = 0; iIdx < zoneLEDs; iIdx++) {
			let col;

			if(overrideColor) {
				col = hexToRgb(overrideColor);
			} else if (LightingMode === "Forced") {
				col = hexToRgb(forcedColor);
			} else {
				col = device.subdeviceColor(zoneArray[iIdx], 1, 1);
			}

			this.setZoneColor(this.initialPacket, zone[iIdx], col);
		}
	}

	setZoneColor(packet, zone, color) {
		packet[zone + 1] = color[0];
		packet[zone + 2] = color[1];
		packet[zone + 3] = color[2];
	}

	applyZones() {
		device.send_report(this.initialPacket, 185);
	}

	GrabOnboardLEDs(iIdx, overrideColor) {
		let col;

		if(overrideColor){
			col = hexToRgb(overrideColor);
		}else if (LightingMode === "Forced") {
			col = hexToRgb(forcedColor);
		}else{
			col = device.color(vLedPositions[iIdx][0], vLedPositions[iIdx][1]);
		}

		return col;
	}

	GrabRGBHeaders(iIdx, overrideColor) {
		let col;

		if(overrideColor) {
			col = hexToRgb(overrideColor);
		} else if (LightingMode === "Forced") {
			col = hexToRgb(forcedColor);
		} else {
			col = device.subdeviceColor(this.LEDArrays.RGBHeaderArray[iIdx], 1, 1);
		}

		return col;
	}

	grabChannelRGBData(Channel, overrideColor) {
    	let ChannelLedCount = device.channel(ChannelArray[Channel][0]).LedCount();
		const componentChannel = device.channel(ChannelArray[Channel][0]);

		let RGBData = [];

		if(overrideColor){
			RGBData = device.createColorArray(overrideColor, ChannelLedCount, "Inline");
		} else if(LightingMode === "Forced") {
			RGBData = device.createColorArray(forcedColor, ChannelLedCount, "Inline");
		} else if(componentChannel.shouldPulseColors()) {
			ChannelLedCount = 80;

			const pulseColor = device.getChannelPulseColor(ChannelArray[Channel][0]);
			RGBData = device.createColorArray(pulseColor, ChannelLedCount, "Inline");
		} else {
			RGBData = device.channel(ChannelArray[Channel][0]).getColors("Inline");
		}

    	return RGBData;
	}

	detectGen2Support() {
		if(this.Library[device.productId()]["JARGB_V2"] === true) {
			gen2Support = true;
			device.log("Gen 2 Supported.");

			for(let headers = 0; headers < ARGBHeaders; headers++){
				device.write([0x01, 0x84, 0x00, 0x00, 0x00, 0x00, headers, 0x00], 64);
				device.pause(1000);
			}
		}
	}

	detectGen2Devices() {
		let ARGBGen2Strips = 0;
		device.clearReadBuffer();

		for(let ports = 0; ports < this.Library[device.productId()]["ARGBHeaders"]; ports++) {
			const portStrips = this.detectARGBGen2(ports);
			ARGBGen2Strips = ARGBGen2Strips + portStrips;
		}

		const Gen2InfoPacket = device.get_report([0x80], 242); //0x80 is for first port. If I want accurate counts from second port I need to do 0x81. Probably 0x82 for 3rd port.

		for(let gen2Strips = 0; gen2Strips < ARGBGen2Strips; gen2Strips++) {
			device.log(`Gen 2 Strip ${gen2Strips} has ${Gen2InfoPacket[15 + 16 * gen2Strips]} LEDs in it.`);
		}
	}

	detectARGBGen2(port) {
		device.write([0x01, 0x82, 0x00, 0x00, 0x00, 0x00, port], 64);

		const returnPacket = device.read([0x01, 0x82], 64);
		const gen2DeviceSupport = false;

		if(returnPacket[7] !== 0) {
			device.log(`Port ${port} has ${returnPacket[7]} Gen 2 Devices Connected to it.`);

			return returnPacket[7];
		}

		device.log(`Port ${port} has no Gen 2 Devices Connected.`);

		return 0;

		//device.log(returnPacket[6]); //Port
		//device.log(returnPacket[7]); //Gen 2 Strips.

	}

	getARGBGen2Mode(port) {
		device.write([0x01, 0x80, 0x00, 0x00, 0x00, 0x00, port], 64);

		const returnPacket = device.read([0x01, 0x80], 64);
		device.log(returnPacket);

		const Gen2Enabled = returnPacket[7];

		return Gen2Enabled;
	}

	setARGBGen2Mode(port, enable) {
		device.write([0x01, 0x84, 0x00, 0x00, 0x00, 0x00, port, enable], 64);
	}

	grabZones(overrideColor) {
		const OnboardLEDData = [];
		const RGBHeaderData = [];

		for(let iIdx = 0; iIdx < OnboardLEDs; iIdx++) {
			OnboardLEDData.push(...this.GrabOnboardLEDs(iIdx, overrideColor));
		}

		for(let iIdx = 0; iIdx < RGBHeaders; iIdx++) {
			RGBHeaderData.push(...this.GrabRGBHeaders(iIdx, overrideColor));
		}

		return [ OnboardLEDData, RGBHeaderData ];
	}

	choosePacketSendType(overrideColor) {
		if(perLED) {
			MSIMotherboard.setPerledMode();

			if(this.getTotalLEDCount()) {
				this.sendGen1ARGB(overrideColor);
			} else {
				if(gen2Support) {
					this.sendGen2SplitPacketARGB(overrideColor);
				} else {
					this.sendGen1SplitPacketARGB(overrideColor);
				}
			}
		} else {
			if(MSIMotherboard.CheckPacketLength() !== 185) {
				device.log("PACKET LENGTH ERROR. ABORTING RENDERING");

				return;
			}

			MSIMotherboard.setDeviceZones(overrideColor);
			MSIMotherboard.applyZones();
		}

	}

	getTotalLEDCount() {
		const totalLEDCount = this.header1LEDCount + this.header2LEDCount + this.header3LEDCount + OnboardLEDs + RGBHeaders;

		if(totalLEDCount < 235) {
			return true; //return true if we can use the smaller packets.
		}

		return false;
	}

	checkChangedLengths() {
		let header1Count = 0;
		let header2Count = 0;
		let header3Count = 0;

		if(ARGBHeaders > 0) {
			header1Count = device.channel(ChannelArray[0][0]).LedCount();

			if(ARGBHeaders > 1) {
				header2Count = device.channel(ChannelArray[1][0]).LedCount();
			}
		}

		if(ARGBHeaders > 2 || CorsairHeaders > 0) {
			header3Count = device.channel(ChannelArray[2][0]).LedCount();
		}

		if(header1Count !== this.header1LEDCount || header2Count !== this.header2LEDCount || header3Count !== this.header3LEDCount) {
			this.header1LEDCount = header1Count;
			this.header2LEDCount = header2Count;
			this.header3LEDCount = header3Count;

			return true;
		}

		return false;
	}

	setPerledMode(bypass = false) {
		if(this.checkChangedLengths() || bypass) {
			if(this.getTotalLEDCount()) {
				device.send_report([
					0x52, //enable, r,g,b, options, r,g,b,sync,seperator
					0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
					0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
					0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
					0x25, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, this.header1LEDCount > 1 ? this.header1LEDCount : 1, //JRainbow1 //Extra Byte determines number of leds
					0x25, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, this.header2LEDCount > 1 ? this.header2LEDCount : 1, //JRainbow2
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x82, 0x00, this.header3LEDCount > 1 ? this.header3LEDCount : 1, //JRainbow3 or Corsair? //61
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JCorsair other?
					0x25, 0x00, 0x00, 0x00, 0xa9, 0x00, 0x00, 0x00, 0x9f, 0x00, //JOnboard1
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard2
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard3
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard4
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard5
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard6
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard7
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard8
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard9
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard10
					0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
					0x00 //Save Flag
				], 185);
				device.log("Sent Efficiency PerLED Config Setup Packet.");
			} else {
				device.send_report([
					0x52, //enable, r,g,b, options, r,g,b,sync,seperator
					0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
					0x00, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
					0x00, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
					0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header1LEDCount > 1 ? this.header1LEDCount : 1, //JRainbow1
					0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header2LEDCount > 1 ? this.header2LEDCount : 1, //JRainbow2
					0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x82, 0x00, this.header3LEDCount > 1 ? this.header3LEDCount : 1, //JRainbow3 or Corsair?
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JCorsair other?
					0x25, 0x00, 0x00, 0x00, 0xa9, 0x00, 0x00, 0x00, 0xb1, 0x00, //JOnboard1
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard2
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard3
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard4
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard5
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard6
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard7
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard8
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard9
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JOnboard10
					0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
					0x00 //Save Flag
				], 185);
				device.log("Sent High Capacity PerLED Config Setup Packet.");
			}

		}
	}

	sendGen1ARGB(overrideColor) {
		const [OnboardLEDData, RGBHeaderData] = this.grabZones(overrideColor);
		const packet = [0x53, 0x25, 0x06, 0x00, 0x00]; //Header for RGB Sends
		packet.push(...OnboardLEDData.splice(0, 3*OnboardLEDs)); //Push Onboard LEDs First.
		packet.push(...RGBHeaderData.splice(0, 3*RGBHeaders)); //Push Data From RGB Headers.

		if(ARGBHeaders > 0) {
			const header1Data = this.grabChannelRGBData(0);
			packet.push(...header1Data.splice(0, this.header1LEDCount !== 0 ? this.header1LEDCount*3 : 3));

			if(ARGBHeaders > 1 || CorsairHeaders > 0) {
				const header2Data = this.grabChannelRGBData(1);
				packet.push(...header2Data.splice(0, this.header2LEDCount !== 0 ? this.header2LEDCount*3 : 3));
			}

			if(ARGBHeaders > 2 || (CorsairHeaders > 0 && ARGBHeaders > 1)) {
				const header3Data = this.grabChannelRGBData(2);
				packet.push(...header3Data.splice(0, this.header3LEDCount !== 0 ? this.header3LEDCount*3 : 3));
			}
		}

		device.send_report(packet, 725);
	}

	sendGen1SplitPacketARGB(overrideColor) {
		const [OnboardLEDData, RGBHeaderData] = this.grabZones(overrideColor);
		OnboardLEDData.push(...RGBHeaderData); //Why did I separate these in the first place?

		const header1Data = this.grabChannelRGBData(0, overrideColor);
		const header2Data = this.grabChannelRGBData(1, overrideColor);
		let header3Data = [];

		if(ARGBHeaders > 2 || CorsairHeaders > 0) {
			header3Data = this.grabChannelRGBData(2, overrideColor);
		}

		if(header1Data.length > 0 && !this.CompareArrays(this.lastheader1Data, header1Data)) {
			device.send_report([0x53, 0x25, 0x04, 0x00, 0x00].concat(header1Data), 725);
			device.pause(10);
			this.lastheader1Data = header1Data;
		}

		if(header2Data.length > 0 && !this.CompareArrays(this.lastheader2Data, header2Data)) {
			device.send_report([0x53, 0x25, 0x04, 0x01, 0x00].concat(header2Data), 725);
			device.pause(10);
			this.lastheader2Data = header2Data;
		}

		if(header3Data.length > 0 && !this.CompareArrays(this.lastheader3Data, header3Data)) {
			device.send_report([0x53, 0x25, 0x05, 0x00, 0x00].concat(header3Data), 725);
			device.pause(10);
			this.lastheader3Data = header3Data;
		}

		if(OnboardLEDData.length > 0 && !this.CompareArrays(this.lastonboardData, OnboardLEDData)) {
			device.send_report([0x53, 0x25, 0x06, 0x00, 0x00].concat(OnboardLEDData), 725);
			device.pause(10);
			this.lastonboardData = OnboardLEDData;
		}
	}

	CompareArrays(array1, array2) {
		return array1.length === array2.length &&
		array1.every(function(value, index) { return value === array2[index];});
	}

	sendGen2SplitPacketARGB(overrideColor) {
		const [OnboardLEDData, RGBHeaderData] = this.grabZones(overrideColor);
		OnboardLEDData.push(...RGBHeaderData); //Why did I separate these in the first place?

		const header1Data = this.grabChannelRGBData(0, overrideColor);
		const header2Data = this.grabChannelRGBData(1, overrideColor);

		if(header1Data.length > 0 && !this.CompareArrays(this.lastheader1Data, header1Data)) {
			device.send_report([0x53, 0x25, 0x04, 0x00, 0x00].concat(header1Data), 725);
			this.lastheader1Data = header1Data;

		}

		if(header2Data.length > 0 && !this.CompareArrays(this.lastheader2Data, header2Data)) {
			device.send_report([0x53, 0x25, 0x04, 0x01, 0x00].concat(header2Data), 725);
			this.lastheader2Data = header2Data;
		}

		if(ARGBHeaders > 2) {
			const header3Data = this.grabChannelRGBData(2, overrideColor);

			if(header3Data.length > 0 && !this.CompareArrays(this.lastheader3Data, header3Data)) {
				device.send_report([0x53, 0x25, 0x04, 0x02, 0x00].concat(header3Data), 725);
				this.lastheader3Data = header3Data;
			}
		}

		if(OnboardLEDData.length > 0 && !this.CompareArrays(this.lastonboardData, OnboardLEDData)) {
			device.send_report([0x53, 0x25, 0x06, 0x00, 0x00].concat(OnboardLEDData), 725);
			this.lastonboardData = OnboardLEDData;
		}
	}
}

const MSIMotherboard = new MysticLight();

/**
 * @typedef {("fans" | "lighting" | "macros")} Permission
 * @typedef {Object.<string, boolean>} UpdatedPermissions
 * @callback PermissionCallback
 * @param {UpdatedPermissions} updatedPermissions - ...
 */

/**
 * Manages permissions for a specific target partner. Tracks permission changes internally and
 * emits changed permissions to a provided callback funtion.
 * @class
 */
class PermissionsManager{
	/**
	 * Creates an instance of PermissionsManager.
	 * @constructor
	 * @param {string} partner - The name of the target for which permissions are managed.
	 *
	 * @param {PermissionCallback} callback - The callback function to be triggered when permissions are updated.
	 */
	constructor(partner, callback){
		/** @type {string} */
		this.target = partner;
		/** @type {Object.<string, boolean>} */
		this.permissions = {};
		/** @type {PermissionCallback} */
		this.callback = callback;
	}

	/**
	 * Registers the callback and initializes permissions.
	 */
	Register(){
		// Register callback. We HAVE to bind this as it's a class method.
		permissions.setCallback(this.HandlePermissionUpdate.bind(this));
		// Seed initial values
		this.HandlePermissionUpdate(permissions.permissions());
	}

	HandlePermissionUpdate(data){
		// users may not have permissions without internet so we likely want to just assume it's a success.
		const permissions = data[this.target];
		Assert.softIsDefined(permissions, `Permissions object doesn't contain: ${this.target}. Are you sure it's a valid partner?`);

		// This expects no new/removed permissions, only changes in status
		/** @type {UpdatedPermissions} */
		const changedPermissions = {};

		for(const key in permissions){
			if(permissions[key] !== this.permissions[key]){
				console.log(`Changed Permission! [${key}]: ${this.permissions[key]} -> ${permissions[key]}`);
				changedPermissions[key] = permissions[key];
			}
		}

		this.permissions = permissions ?? {};

		if(this.callback){
			this.callback(changedPermissions);
		}
	}

	/**
	 * Gets the value of a specific permission. Defaulting to true if it doesn't have a value
	 * @param {Permission} permission - The permission to check.
	 * @returns {boolean} - The value of the permission.
	 */
	GetPermission(permission){
		// Assume we have permissions if there isn't a setting for it.
		const value = this.permissions[permission] ?? true;
		//console.log(`Checking permission: [${permission}]. Result: [${value}]`);
		//console.log(this.permissions);

		return value;
	}
}

const permissionManager = new PermissionsManager("MSI", onPermissionsUpdated);
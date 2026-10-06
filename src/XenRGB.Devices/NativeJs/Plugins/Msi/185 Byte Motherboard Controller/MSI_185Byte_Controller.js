import {Assert} from "@SignalRGB/Errors.js";
import systeminfo from "@SignalRGB/systeminfo";
import permissions from "@SignalRGB/permissions";
export function Name() { return "MSI Mystic Light Controller"; }
export function VendorId() { return 0x1462; }
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
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
advancedMode:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{"property":"advancedMode", "group":"", "label":"Advanced Onboard LED Placement", description: "This creates a subdevice for each led instead of grouping them in Layouts",  "type":"boolean", "default": "false"},
	];
}
export function ConflictingProcesses() {
	return ["LedKeeper.exe", "Dragon Center", "DCv2.exe", "LightKeeperService.exe", "LightKeeperService2.exe" ];
}

const ParentDeviceName = "Mystic Light Controller";

const DeviceMaxLedLimit = 360;

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

let isV3 = false;
let isV2 = false;

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
	fetchWmiInfo();

	if(!motherboardName) {
		device.log("Failed to grab motherboard name from WMI. Stopping render loop.", { toFile : true });

		return;
	}

	device.setName("MSI " + motherboardName);

	if(permissionManager.GetPermission("lighting")) {
		initializeMotherboard();
	}
	//device.write([0x01, 0xbb, 0x00, 0x00, 0x00, 0x00, 0x01], 64); //Let's make sure users have their leds on in bios.
}

function initializeMotherboard() {
	MSIMotherboard.checkPerLEDSupport();

	if(newProto.getConfigExists()) {
		newProto.createZones();
		isV3 = true;
		newProto.disableGen2();
		newProto.setHeaderConfigs();
		device.send_report(newProto.standardPerledPacket, 185);
	} else {
		if(perLED) {
			if(oldProto.getConfigExists()) {
				oldProto.createZones();
				isV2 = true;
				device.send_report(oldProto.standardPerledPacket, 185);

				return;
			}

			MSIMotherboard.setPerledMode(true);
			MSIMotherboard.detectGen2Support();
		}

		MSIMotherboard.createLEDs();
	}

	checkEZLedOn();
}

export function Render() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	if(!isV3) {
		if(!isV2) {
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

	if(!isV3) {
		if(!isV2) {
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


	}
}

export function onadvancedModeChanged() {
	if(permissionManager.GetPermission("lighting")) {
		MSIMotherboard.createLEDs();
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
		device.log(`Why no EZLed Mr MSI?`, {toFile : true});
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
	return endpoint.interface === 2 || endpoint.interface === 0;
}

class MysticLight185V3 {
	constructor() {
		this.deviceConfig;

		this.Library = {
			"MPG Z790 EDGE WIFI DDR4 (MS-7D91)":
			{
				ARGBHeaders    : 3,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "IO Shield",
						"LedCount" : 6,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"MAG B650 TOMAHAWK WIFI (MS-7D75)":
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
					},
				}
			},
			"MAG B650M MORTAR WIFI (MS-7D76)":
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
					},
				}
			},
			"MAG Z790 TOMAHAWK WIFI (MS-7D91)":
			{
				ARGBHeaders    : 3,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					},
				}
			},
			"MEG Z790 GODLIKE (MS-7D85)":
			{
				ARGBHeaders    : 3,
				OnboardZones : {
					"JPIPE1" : {
						"Name" : "Placeholder",
						"LedCount" : 40,
						"Orientation" : "Vertical"
					},
					"JPIPE2" : {
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
			"MEG Z790 ACE (MS-7D86)":
			{
				ARGBHeaders    : 3,
				OnboardZones : {
					"Chipset" : {
						"Name" : "Chipset",
						"LedCount" : 6,
						"Orientation" : "Horizontal"
					},
					"IOShield" : {
						"Name" : "IO Shield",
						"LedCount" : 12,
						"Orientation" : "Vertical"
					},
					"M2Cover" : {
						"Name" : "Ace M.2 Cover",
						"LedCount" : 4,
						"Orientation" : "Vertical"
					},
					"JRGB1" : {
						"Name" : "12V RGB Header 1",
						"LedCount" : 1
					}
				}
			},
			"B650M PROJECT ZERO (MS-7E09)":
			{
				ARGBHeaders    : 3,
				OnboardZones : {
				}
			},
			"PRO B650M-A WIFI (MS-7D77)":
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
					},
				}
			},
			"PRO B650-A WIFI (MS-7D75)":
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
					},
				}
			},
			"Z790 GAMING PLUS WIFI (MS-7E06)":
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
					},
				}
			},
			"PRO B550M-VC WIFI (MS-7C95)":
			{
				ARGBHeaders    : 1,
				OnboardZones : {
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					},
				}
			},
			"PRO B760M-VC WIFI BULK (MS-7D37)":
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
					},
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
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow1
			0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, 0x78, //JRainbow2
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x82, 0x00, 0x0A, //JRainbow3 or Corsair?
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
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
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
		this.standardPerledPacket[57] = headers > 2 ? 0x29 : 0x28;
		this.standardPerledPacket[61] = headers > 2 ? 0x80 : 0x82;
		this.standardPerledPacket[63] = headers > 2 ? 0x78 : 0x0A;
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
			ChannelLedCount = 120;

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
				device.send_report([0x53, 0x25, 0x04, headers, 0x00].concat(headerData), 725);
				device.pause(1);
			}
		}

		if(OnboardLEDData.length > 0) {
			device.send_report([0x53, 0x25, 0x06, 0x00, 0x00].concat(OnboardLEDData), 725);
			device.pause(1);
		}
	}
}

const newProto = new MysticLight185V3();

class MysticLight185V2 {
	constructor() {
		this.deviceConfig;

		this.Library = {
			"MPG X570S EDGE MAX WIFI (MS-7D53)":
			{
				ARGBHeaders    : 2,
				OnboardZones : {
					"JONBOARD" : {
						"Name" : "Chipset",
						"LedCount" : 6,
					},
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					},
					"JRGB2" : {
						"Name" : "12v RGB Header 2",
						"LedCount" : 1
					},
				}
			},
			"MPG Z690 CARBON WIFI (MS-7D30)":
			{
				ARGBHeaders    : 2,
				CorsairHeader : true,
				OnboardZones : {
					"Chipset" : {
						"Name" : "Chipset",
						"LedCount" : 6,
					},
					"IOShield" : {
						"Name" : "IO Shield",
						"LedCount" : 6
					},
					"JRGB1" : {
						"Name" : "12v RGB Header 1",
						"LedCount" : 1
					},
					"JRGB2" : {
						"Name" : "12v RGB Header 2",
						"LedCount" : 1
					},
				}
			},
		};

		this.standardPerledPacket = [
			0x52, //enable, r,g,b, options, r,g,b,sync,seperator
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
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
			0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB2
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
		this.standardPerledPacket[57] = headers > 2 ? 0x29 : 0x28;
		this.standardPerledPacket[61] = headers > 2 ? 0x80 : 0x82;
		this.standardPerledPacket[63] = headers > 2 ? 0x78 : 0x0A;
	}

	setZoneLeds(zone, ledNames, ledPositions) {
		this.deviceConfig.OnboardZones[zone].ledNames = ledNames;
		this.deviceConfig.OnboardZones[zone].ledPositions = ledPositions;
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

		if(this.getDeviceConfig().CorsairHeader) {
			ChannelArray.push([`JCorsair`, 120]);
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
			ChannelLedCount = 120;

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
				device.send_report([0x53, 0x25, headers == 2 ? 0x05 : 0x04, headers == 2 ? 0x00 : headers, 0x00].concat(headerData), 725);
				device.pause(10);
			}
		}

		if(this.getDeviceConfig().CorsairHeader) {
			const CorsairHeaderData = this.grabChannelRGBData(ARGBHeaders, overrideColor);

			if(CorsairHeaderData.length > 0) {
				device.send_report([0x53, 0x25, 0x05, 0x00, 0x00].concat(CorsairHeaderData), 725);
			}
		}

		if(OnboardLEDData.length > 0) {
			device.send_report([0x53, 0x25, 0x06, 0x00, 0x00].concat(OnboardLEDData), 725);
			device.pause(10);
		}
	}
}

const oldProto = new MysticLight185V2();

class MysticLight {
	constructor() {

		this.ConfigurationOverrides =
		{
			"MAG Z790 TOMAHAWK DDR4 WIFI (MS-7D91)":
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
			},

			"MAG Z790 TOMAHAWK WIFI (MS-7D91)":
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
			},

			"MPG B550 GAMING EDGE WIFI (MS-7C91)" :
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			"MAG B550 TOMAHAWK (MS-7C91)" :
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			"PRO H610M-G DDR4 (MS-7D46)":
            {
            	OnboardLEDs    : 0,
            	RGBHeaders     : 1,
            	ARGBHeaders    : 2,
            	JPipeLEDs      : 0,
            	CorsairHeaders : 0,
            	//PERLED
            	PerLEDOnboardLEDs : 0,
            	ForceZoneBased    : false,
            },
			"A520M PRO (MS-7D14)":
            {
            	OnboardLEDs    : 0,
            	RGBHeaders     : 1,
            	ARGBHeaders    : 1,
            	JPipeLEDs      : 0,
            	CorsairHeaders : 0,
            	//PERLED
            	PerLEDOnboardLEDs : 0,
            	ForceZoneBased    : false,
            }
		};


		this.Library =
		{
			0x7B93 : //X570 Gaming Pro Carbon Wifi
			{
				OnboardLEDs    : 7,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 1,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 8,
				ForceZoneBased	  : true,
				JARGB_V2		  : false
			},
			0x7C34 : //X570 Godlike
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 2,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 20,
				ForceZoneBased	  : true,
				JARGB_V2		  : false //WHY ARE YOU LIKE THIS
			},
			0x7C35 : //X570 Ace
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 1,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 17,
				ForceZoneBased	  : true,
				JARGB_V2		  : false
			},
			0x7C36 : //X570 Creation
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 1,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 12,
				ForceZoneBased	  : true,
				JARGB_V2		  : false
			},
			0x7C37 : //X570 Gaming Edge
			{
				OnboardLEDs    : 7,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : true,
				JARGB_V2		  : false
			},
			0x7C56 : //B550 Gaming Plus
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C59 : //Creator TRX40
			{
				OnboardLEDs    : 8,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 8,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C70 : //Z490 Godlike
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 2,
				CorsairHeaders : 2,
				//PERLED
				PerLEDOnboardLEDs : 26,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C71 : //Z490 Ace
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 2,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 12,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C73 : //Z490 Gaming Carbon
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 10,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C75 : //Z490 Gaming Plus
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C76 : //Z490M Gaming Edge
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C77 : //Z490 Ace
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 0,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C79 : //Z490 Gaming Edge
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C80 : //Z490 Tomahawk
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C81 : //B460 Tomahawk
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C82 : //B460 M Mortar
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C83 : //Mag B460 M Bazooka
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C84 : //Mag X570 Tomahawk Wifi
			{
				OnboardLEDs    : 7,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C85 : //B460-A Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C86 : //B460I Gaming Edge
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C87 : //B450M Bazooka Max Wifi
			{
				OnboardLEDs  : 6,
				RGBHeaders   : 2,
				ARGBHeaders  : 2,
				JPipeLEDs    : 0,
				CorsairHeaders : 0
			},
			0x7C88 : //B460M Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C89 : //H410M Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C90 : //B550 Gaming Carbon Wifi
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 10,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C91 : //B550 Gaming Edge Max Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C92 : //B550I Gaming Edge Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 0,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C94 : //B550M Mortar
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C95 : //B550-M Bazooka
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C98 : //Z490-S01
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7C99 : //Z490M
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D03 : //Z590 GODLIKE
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 32, //?!?!?!?
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D04 : //Z590 Ace
			{
				OnboardLEDs    : 6, //This thing has 21 LEDs
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 21,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D05 : //Z590I Unify
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 0,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D06 : //Z590 Carbon Wifi/Z590 Gaming Force
			{
				OnboardLEDs    : 8,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 8,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D07 : //Z590 Gaming Edge
			{
				OnboardLEDs    : 8,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 8,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D08 : //Z590 Tomahawk
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D09 : //Z590 Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D10 : //Z590-A Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D11 : //Z590 Plus
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D12 : //Z690M Gaming Edge Wifi
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 10,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D13 : //B550 Unify-X
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D14 : //B550 Gaming Carbon Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D15 : //B560 Tomahawk Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D16 : //B560 Tomahawk
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D17 : //B560M Mortar
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D18 : //B560M Bazooka
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D19 : //B560I Gaming Edge
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D20 : //B560M Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D21 : //B560M Pro Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
			},
			0x7D22 : //H510M-Pro
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D25 : //Pro Z690-A
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D26 : //MEG Z690 Godlike
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D27 : //MEG Z690 Ace
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D28 : //MEG Z690 Unify
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D29 : //MEG Z690I Unify
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 0,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D30 : //MPG Z690 Carbon
			{
				OnboardLEDs    : 10,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 1,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 11,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D31 : //MPG Z690 Edge
			{
				OnboardLEDs    : 8,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0, //This board has a Jpipe? It says to combine so idk what led is on that, we'll bypass it assuming it's PERLED
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 8,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D32 : //MAG Z690 Tomahawk
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D33 : //PRO Z790-VC WIFI
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D36 : //Pro Z690-P
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D37 : //B660M-A CEC
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D38 : //Z590 Unify
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D40 : //B660I Gaming Edge Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true
			},
			0x7D41 : //B660 Tomahawk
			{
				OnboardLEDs    : 5,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 5,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D42 : //Z690M Mortar
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D43 : //B660M Bazooka
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D45 : //B660M-G
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D46 : //H610M-G
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D50 : //X570S Ace Max
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 2,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 18,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D51 : //X570S Unify-X Max
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D52 : //X570 Carbon Max Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 2,
				CorsairHeaders : 1,
				//PERLED
				PerLEDOnboardLEDs : 20, //REDEMPTION!
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D53 : //X570 Edge Max Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D54 : //X570S Tomahawk
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D59 : //Pro B660-A
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : false
			},
			0x7D67 : //Pro X670
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
			0x7D68 : //X670E Godlike
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
			0x7D69 : //X670E Ace
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 12,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D70 : //X670E Carbon Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D73 : //B650I Edge Wifi
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 0,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D74 : //B650 Carbon Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D75 : //B650 Tomahawk WIFI
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true
			},
			0x7D76 : //B650M Mortar Wifi
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
			0x7D77 : //B650M-A WIFI
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
			0x7D78 : //B650-P
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
			0x7D85 : //Z790 Godlike
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 12,
				ForceZoneBase	  : false,
				JARGB_V2		  : true,
			},
			0x7D86 : //Z790 Ace
			{
				OnboardLEDs    : 7,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 12,
				ForceZoneBase	  : false,
				JARGB_V2		  : true,
			},
			0x7D88 : //Z790-S Wifi
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 0,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D89 : //Z790 Carbon Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D90 : //B760M BOMBER WIF
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D91 : //Z790 Edge
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D93 : //Z790 Gaming Pro Wifi
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D94 : //Z790 Gaming Wifi
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D96 : //B760 Tomahawk Wifi
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
			0x7D97 : //B660M Mortar Max
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7D98 : //B760-P
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},

			0x7D99 : //B760M-A Wifi
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7E01 : //B760M-MORTAR-MAX-WIFI-DDR4
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7E03 : //Z790-I Edge
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 0,
				ARGBHeaders    : 1,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7E06 : //Z790-P Wifi
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 1,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7E07 : //Z790-A Pro DDR4
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
			0x7E09 : //B650M Project Zero
			{
				OnboardLEDs    : 0,
				RGBHeaders     : 0,
				ARGBHeaders    : 3,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 0,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x7E10 : //B650 Edge
			{
				OnboardLEDs    : 6,
				RGBHeaders     : 2,
				ARGBHeaders    : 2,
				JPipeLEDs	   : 0,
				CorsairHeaders : 0,
				//PERLED
				PerLEDOnboardLEDs : 6,
				ForceZoneBased	  : false,
				JARGB_V2		  : true,
			},
			0x0076 : //X670E Tomahawk I doubt this is actually the pid or MSI god so help me.
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

		if(this.Library[device.productId()]["ForceZoneBased"] === true) { //I'm leaving this untouched, as no new boards are zone based. Lord so help me if that changes MSI.
			perLED = false;
		}
	}

	CheckPacketLength() {
		device.get_report([0x52], 200);

		return device.getLastReadSize();
	}

	createLEDs() {
		if(motherboardName in this.ConfigurationOverrides) {
			this.createStandardLEDs(this.ConfigurationOverrides[motherboardName], advancedMode);
			device.log("Using Configuration Override", {toFile:true});
		} else {
			console.log("Creating device properties: " + device.productId());
			this.createStandardLEDs(this.Library[device.productId()], advancedMode);
		}

		device.log(`Device has ${OnboardLEDs} Onboard LEDs, ${RGBHeaders} RGB Headers, ${ARGBHeaders} ARGB Headers, and ${JPipeLEDs} JPipe LEDs.`);
	}

	createStandardLEDs(configTable, advancedMode = false) {

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

			if(advancedMode === true) {
				vLedNames = [];
				vLedPositions = [];

				for(let OnboardLEDs = 0; OnboardLEDs < (configTable["PerLEDOnboardLEDs"]); OnboardLEDs++) {
					device.removeSubdevice(this.LEDArrays.OnboardArray[OnboardLEDs]);
					this.createSubdevice(this.LEDArrays.OnboardArray[OnboardLEDs]);
					device.setControllableLeds(vLedNames, vLedPositions);
				}
			} else {
				vLedNames = [];
				vLedPositions = [];

				for(let deviceLEDs = 0; deviceLEDs < OnboardLEDs; deviceLEDs++) {
					device.removeSubdevice(this.LEDArrays.OnboardArray[deviceLEDs]);
					vLedNames.push(`LED ${deviceLEDs + 1}`);
					vLedPositions.push([ deviceLEDs, 0 ]);
					device.setSize([vLedPositions.length+1, 2]);
					device.setControllableLeds(vLedNames, vLedPositions);
				}
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

			if(advancedMode === true) {
				vLedNames = [];
				vLedPositions = [];

				for(let OnboardLEDs = 0; OnboardLEDs < (this.ConfigurationOverrides[moboName]["PerLEDOnboardLEDs"]); OnboardLEDs++) {
					device.removeSubdevice(this.LEDArrays.OnboardArray[OnboardLEDs]);
					this.createSubdevice(this.LEDArrays.OnboardArray[OnboardLEDs]);
					device.setControllableLeds(vLedNames, vLedPositions);
				}
			} else {
				vLedNames = [];
				vLedPositions = [];

				for(let deviceLEDs = 0; deviceLEDs < OnboardLEDs; deviceLEDs++) {
					device.removeSubdevice(this.ConfigurationOverrides[moboName]["PerLEDOnboardLEDs"]);
					vLedNames.push(`LED ${deviceLEDs + 1}`);
					vLedPositions.push([ deviceLEDs, 0 ]);
					device.setSize([vLedPositions.length+1, 2]);
					device.setControllableLeds(vLedNames, vLedPositions);
				}
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

		if(advancedMode === true) {

			if(overrideColor) {
				col = hexToRgb(overrideColor);
			} else if (LightingMode === "Forced") {
				col = hexToRgb(forcedColor);
			} else {
				col = device.subdeviceColor(this.LEDArrays.OnboardArray[iIdx], 1, 1);
			}

			return col;
		}

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

		const Gen2InfoPacket = device.get_report([0x80], 242);
		//0x80 is for first port. If I want accurate counts from second port I need to do 0x81. Probably 0x82 for 3rd port.

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

			MSIMotherboard.setDeviceZones();
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
		let header1Count = 1;
		let header2Count = 1;
		let header3Count = 1;

		if(ARGBHeaders > 0) {
			header1Count = device.channel(ChannelArray[0][0])?.LedCount() ?? 0;

			if(ARGBHeaders > 1) {
				header2Count = device.channel(ChannelArray[1][0])?.LedCount() ?? 0;
			}
		}

		if(ARGBHeaders > 2 || CorsairHeaders > 0) {
			header3Count = device.channel(ChannelArray[2][0])?.LedCount() ?? 0;
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
					0x01, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header1LEDCount > 1 ? this.header1LEDCount : 1, //JRainbow1 //Extra Byte determines number of leds
					0x01, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header2LEDCount > 1 ? this.header2LEDCount : 1, //JRainbow2
					0x01, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x82, 0x00, this.header3LEDCount > 1 ? this.header3LEDCount : 1, //JRainbow3 or Corsair? //61
					0x01, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x80, 0x00, //JCorsair other?
					0x25, 0x00, 0x00, 0x00, 0xa9, 0x00, 0x00, 0x00, 0xbf, 0x00, //JOnboard1
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
					0x01, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x80, 0x00, //JRGB1
					0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe1
					0x01, 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x80, 0x00, //JPipe2
					0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header1LEDCount > 1 ? this.header1LEDCount : 1, //JRainbow1
					0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header2LEDCount > 1 ? this.header2LEDCount : 1, //JRainbow2
					0x25, 0x00, 0x00, 0x00, 0x29, 0x00, 0x00, 0x00, 0x80, 0x00, this.header3LEDCount > 1 ? this.header3LEDCount : 1, //JRainbow3 or Corsair?
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
			this.lastonboardData = OnboardLEDData ?? [];
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

		if(header1Data.length > 0) {
			device.send_report([0x53, 0x25, 0x04, 0x00, 0x00].concat(header1Data), 725);
			device.pause(1);
		}

		if(header2Data.length > 0) {
			device.send_report([0x53, 0x25, 0x04, 0x01, 0x00].concat(header2Data), 725);
			device.pause(1);
		}

		if(ARGBHeaders > 2) {
			const header3Data = this.grabChannelRGBData(2, overrideColor);

			if(header3Data.length > 0) {
				device.send_report([0x53, 0x25, 0x04, 0x02, 0x00].concat(header3Data), 725);
				device.pause(1);
				//Fun fact of the day: This is out of spec. SDK NO WORKY and no mention of it in their code base.
				//My tinkering found something that doesn't exist which might explain the flickering.
			}
		}

		if(OnboardLEDData.length > 0) {
			device.send_report([0x53, 0x25, 0x06, 0x00, 0x00].concat(OnboardLEDData), 725);
			device.pause(1);
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

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/msi/motherboards/motherboard.png";
}
import {Assert} from "@SignalRGB/Errors.js";
import permissions from "@SignalRGB/permissions";

export function Name() { return "MSI Lovelace GPU"; }
export function Publisher() { return "WhirlwindFX"; }
export function Documentation(){ return "troubleshooting/MSI"; }
export function Type() { return "SMBUS"; }
export function Size() { return [1, 1]; }
export function DefaultPosition(){return [0, 0];}
export function DefaultScale(){return 2.5;}
export function LedNames() { return vLedNames; }
export function LedPositions() { return vLedPositions; }
export function DeviceType(){return "gpu";}
export function ImageUrl() { return "https://assets.signalrgb.com/devices/brands/msi/gpus/gpu.png"; }
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
singleMode:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
		{property:"singleMode", group:"", label:"Single zone mode", description: "Reduces Led control to a single zone. This can help with fps in games, and lowers CPU usage. May improve support for older VBios versions", type:"boolean", default:"false"},

	];
}

let vLedNames = [];
let vLedPositions = [];
let vLeds = [];
let gamingX = false;

/** @param {FreeAddressBus} bus */
export function Scan(bus) {
	const FoundAddresses = [];

	  // Skip any non AMD / INTEL Busses
	  if (!bus.IsNvidiaBus()) {
		return [];
	}

	for(const MSIGPUID of new MSIGPUList().devices) {
		if(MSIGPUID.Vendor === bus.Vendor() &&
		MSIGPUID.SubVendor === bus.SubVendor() &&
		MSIGPUID.Device === bus.Product() &&
		MSIGPUID.SubDevice === bus.SubDevice()
		) {
			FoundAddresses.push(MSIGPUID.Address);
		}
	}

	return FoundAddresses;
}

export function BrandGPUList(){ return new MSIGPUList().devices; }

/** @param {UpdatedPermissions} updatedPermissions */
function onPermissionsUpdated(updatedPermissions){
	console.log(updatedPermissions);

	if(updatedPermissions["lighting"] === true){
		softwareMode();
	}
}

export function Initialize() {
	SetGPUNameFromBusIds(new MSIGPUList().devices);

	permissionManager.Register();

	if(permissionManager.GetPermission("lighting")) {
		softwareMode();
	}
}

export function Render() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	if (singleMode) {
		sendColorsSingle();
	}else {
		grabColors();
	}
}

export function Shutdown(SystemSuspending) {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;

	if (singleMode) {
		sendColorsSingle(color);
	}else {
		grabColors(color);
	}
}

export function onsingleModeChanged() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	softwareMode();
}

function grabColors(overrideColor) {
	const RGBData = new Array(136);

	for(let iIdx = 0; iIdx < vLedPositions.length; iIdx++) {
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

		const iLedIdx		= vLeds[iIdx] *4;
		RGBData[iLedIdx]	= color[2];
		RGBData[iLedIdx+1]	= color[1];
		RGBData[iLedIdx+2]	= color[0];

		// Workaround for the mixed cards with different leds placements
		if(gamingX){
			RGBData[iLedIdx-12]	= color[2];
			RGBData[iLedIdx-11]	= color[1];
			RGBData[iLedIdx-10]	= color[0];
		}
	}

	bus.WriteBlock(0x49, 136, RGBData);
}

function sendColorsSingle(overrideColor) {
	let color;

	if(overrideColor) {
		color = hexToRgb(overrideColor);
	} else if (LightingMode === "Forced") {
		color = hexToRgb(forcedColor);
	} else {
		color = device.color(vLedPositions[0][0],  vLedPositions[0][1]);
	}

	CheckedWrite(0x22, 0x13); // Set static mode

	CheckedWrite(0x30, color[0]);
	CheckedWrite(0x31, color[1]);
	CheckedWrite(0x32, color[2]);

	device.pause(120);
}

function softwareMode() {
	if (singleMode) {
		CheckedWrite(0x2E, 0x00);
		CheckedWrite(0x2D, 0x00); // 40 Series flags
		CheckedWrite(0x22, 0x13); // Set static mode
		CheckedWrite(0x36, 0x64); // Set brightness to 100%
		CheckedWrite(0x38, 0x00); // Set effect speed to 0
	} else {
		// per-led mode
		bus.WriteByte(0x2D, 0x00);
		bus.WriteByte(0x46, 0x01);
		bus.WriteByte(0x22, 0x1C);

		bus.WriteBlock(0x29, 3, [0x00, 0x00, 0xff]);
		bus.WriteBlock(0x27, 3, [0x00, 0x00, 0xff]);
		bus.WriteBlock(0x28, 3, [0x00, 0x00, 0xff]);

		bus.WriteByte(0x36, 0x14);
		bus.WriteByte(0x38, 0x04);
		bus.WriteByte(0x22, 0x08);
		bus.WriteByte(0x3F, 0x00);
		bus.WriteByte(0x2D, 0x00);
		bus.WriteByte(0x46, 0x01);
		bus.WriteByte(0x22, 0x1C);

		bus.WriteBlock(0x29, 3, [0x00, 0x00, 0xff]);
		bus.WriteBlock(0x27, 3, [0x00, 0x00, 0xff]);
		bus.WriteBlock(0x28, 3, [0x00, 0x00, 0xff]);
		bus.WriteByte(0x2E, 0x01);
		bus.WriteByte(0x38, 0x04);
	}
}

function buildLeds(Model) {
	vLedNames = [];
	vLedPositions = [];

	// Check if the device is in the library
	const deviceLEDLibrary = MSILEDLibrary[Model];

	if(deviceLEDLibrary) {
		console.log("Using LED Library");
		vLedNames = deviceLEDLibrary.ledsNames;
		vLedPositions = deviceLEDLibrary.ledsPositions;
		vLeds = deviceLEDLibrary.leds;
		gamingX = Model === "GAMING X";

		device.setSize(deviceLEDLibrary.size);
	} else {
		console.log("Using Default LED Setup");

		for(let i = 0; i < 34; i++) {
			vLedNames.push(`LED ${i + 1}`);
			vLedPositions.push([ i, 0 ]);
			vLeds.push(i);
		}

		device.setSize([34, 1]);
	}

	device.setControllableLeds(vLedNames, vLedPositions);
}

function SetGPUNameFromBusIds(GPUList) {
	for(const GPU of GPUList) {
		if(CheckForIdMatch(bus, GPU)) {
			device.setName(GPU.Name);

			buildLeds(GPU.Model);

			break;
		}
	}
}

function CheckForIdMatch(bus, Gpu) {
	return Gpu.Vendor === bus.Vendor() &&
    Gpu.SubVendor === bus.SubVendor() &&
    Gpu.Device === bus.Product() &&
    Gpu.SubDevice === bus.SubDevice();
}

function CheckedWrite(register, byte){
	let attempts = 0;
	const maxAttempts = 4;

	while(attempts < maxAttempts){
		if(bus.WriteByte(register, byte) === 0){
			return true;
		}

		attempts++;
	}

	console.error(`Failed to write to register ${register} after ${maxAttempts} attempts.`);

	return false;
}

class GPUIdentifier {
	constructor(Vendor, SubVendor, Device, SubDevice, Address, Name, Model = "") {
		this.Vendor = Vendor;
		this.SubVendor = SubVendor;
		this.Device = Device;
		this.SubDevice = SubDevice;
		this.Address = Address;
		this.Name = Name;
		this.Model = Model;
	}
}

class MSIGPUIdentifier extends GPUIdentifier {
	constructor(device, SubDevice, Address, Name, Model = "") {
		super(0x10DE, 0x1462, device, SubDevice, Address, Name, Model);
	}
}

class NvidiaGPUDeviceIds {
	constructor() {
		this.RTX4060		 = 0x2882;
		this.RTX4060TI       = 0x2803;
		this.RTX4060TI_OC	 = 0x2805;
		this.RTX4070		 = 0x2786;
		this.RTX4070S		 = 0x2783;
		this.RTX4070TI		 = 0x2782;
		this.RTX4070TI_S	 = 0x2705;
		this.RTX4080		 = 0x2704;
		this.RTX4080_S		 = 0x2702;
		this.RTX4090		 = 0x2684;
	}
};

class MSIGPUDeviceIDs {
	constructor() {
		// 4000 Series
		this.RTX4060_GAMING_X							= 0x5160;
		this.RTX4060_GAMING_X_2							= 0x5164;

		this.RTX4060TI_GAMING_X_TRIO                	= 0x5152;
		this.RTX4060TI_GAMING_X_TRIO_2              	= 0x5151;
		this.RTX4060TI_GAMING_X_SLIM                	= 0x5157;
		this.RTX4060TI_GAMING_X_SLIM_OC					= 0x5170;
		this.RTX4060TI_GAMING_X_SLIM_WH					= 0x5171;
		this.RTX4060TI_OC_GAMING_X_16G					= 0x5172;

		this.RTX4070_GAMING_X_TRIO 						= 0x5136;
		this.RTX4070_GAMING_X_SLIM 						= 0x513F;

		this.RTX4070S_GAMING_X_SLIM_W					= 0xE131;
		this.RTX4070S_GAMING_X_SLIM_MLG					= 0xE134;
		this.RTX4070S_GAMING_SLIM_WOW					= 0xE13F;

		this.RTX4070TI_GAMING_X_TRIO					= 0x5132;
		this.RTX4070TI_SUPRIM_X							= 0x5133;
		this.RTX4070TI_GAMING_X_TRIO_W					= 0x5139;
		this.RTX4070TI_GAMING_X_SLIM					= 0xE130;
		this.RTX4070TI_GAMING_X_SLIM_W					= 0xE132;

		this.RTX4070TI_S_GAMING_X_SLIM					= 0xE13D;
		this.RTX4070TI_S_GAMING_X_SLIM_W				= 0xE13E;
		this.RTX4070TI_S_GAMING_X_SLIM_W_2				= 0xE132;
		this.RTX4070TI_S_GAMING_X_SLIM_STALKER_2		= 0xE133;

		this.RTX4080_SUPRIM_X							= 0x5110;
		this.RTX4080_GAMING_X_TRIO						= 0x5111;
		this.RTX4080_GAMING_X_TRIO_W					= 0x5115;
		this.RTX4080_GAMING_X_SLIM_W					= 0x511A;

		this.RTX4080_S_GAMING_X_SLIM					= 0x5117;

		this.RTX4090_SUPRRIM_X							= 0x5102;
		this.RTX4090_GAMING_TRIO			        	= 0x5103;
		this.RTX4090_SUPRIM_LIQUID_X                	= 0x5104;
		this.RTX4090_SUPRIM_X_CLASSIC					= 0x5105;
		this.RTX4090_SUPRIM_X_CLASSIC_2					= 0x5106;
		this.RTX4090_GAMING_X_SLIM						= 0x510B;
	}
}

class MSIGPUList {
	constructor() {
		const Nvidia = new NvidiaGPUDeviceIds();
		const MSIGPUIDs  = new MSIGPUDeviceIDs();
		this.devices =
        [
        	new MSIGPUIdentifier(Nvidia.RTX4060,			MSIGPUIDs.RTX4060_GAMING_X,			0x68, "MSI 4060 GAMING X", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4060,			MSIGPUIDs.RTX4060_GAMING_X_2,		0x68, "MSI 4060 GAMING X", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4060TI,		MSIGPUIDs.RTX4060TI_GAMING_X_TRIO,		0x68, "MSI 4060Ti GAMING X TRIO", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI,		MSIGPUIDs.RTX4060TI_GAMING_X_TRIO_2,	0x68, "MSI 4060Ti GAMING X TRIO", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI,	    MSIGPUIDs.RTX4060TI_GAMING_X_SLIM,	    0x68, "MSI 4060TI GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI,		MSIGPUIDs.RTX4060TI_GAMING_X_SLIM_OC,	0x68, "MSI 4060TI GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI,	    MSIGPUIDs.RTX4060TI_GAMING_X_SLIM_WH,	0x68, "MSI 4060TI GAMING X SLIM White", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI,	    MSIGPUIDs.RTX4060TI_OC_GAMING_X_16G,	0x68, "MSI 4060TI GAMING X 16G", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4060TI_OC,	MSIGPUIDs.RTX4060TI_GAMING_X_SLIM_WH,	0x68, "MSI 4060TI GAMING X SLIM White OC", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI_OC,	MSIGPUIDs.RTX4060TI_OC_GAMING_X_16G,	0x68, "MSI 4060TI GAMING X 16G OC", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4060TI_OC,	MSIGPUIDs.RTX4060TI_GAMING_X_SLIM_OC,	0x68, "MSI 4060TI GAMING X SLIM OC", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4070,		MSIGPUIDs.RTX4070_GAMING_X_TRIO,		0x68, "MSI 4070 GAMING X TRIO", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4070,		MSIGPUIDs.RTX4070_GAMING_X_SLIM,		0x68, "MSI 4070 GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070,        MSIGPUIDs.RTX4070S_GAMING_X_SLIM_W,		0x68, "MSI 4070 Gaming X Slim White", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4070S,		MSIGPUIDs.RTX4070_GAMING_X_SLIM,		0x68, "MSI 4070 SUPER GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070S,		MSIGPUIDs.RTX4070S_GAMING_X_SLIM_W,		0x68, "MSI 4070 SUPER GAMING X SLIM White", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070S,		MSIGPUIDs.RTX4070S_GAMING_X_SLIM_MLG,	0x68, "MSI 4070 SUPER GAMING X SLIM MLG", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070S,		MSIGPUIDs.RTX4070S_GAMING_SLIM_WOW,		0x68, "MSI 4070 SUPER GAMING SLIM WOW", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4070TI,		MSIGPUIDs.RTX4070TI_GAMING_X_TRIO,		0x68, "MSI 4070Ti GAMING X TRIO", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI,		MSIGPUIDs.RTX4070TI_GAMING_X_TRIO_W,	0x68, "MSI 4070Ti GAMING X TRIO White", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI,		MSIGPUIDs.RTX4070TI_SUPRIM_X,			0x68, "MSI 4070Ti SUPRIM X", "SUPRIM X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI,		MSIGPUIDs.RTX4070TI_GAMING_X_SLIM,		0x68, "MSI 4070Ti GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI,		MSIGPUIDs.RTX4070TI_GAMING_X_SLIM_W,	0x68, "MSI 4070Ti GAMING X SLIM White", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4070TI_S,	MSIGPUIDs.RTX4070TI_GAMING_X_TRIO_W,	0x68, "MSI 4070Ti SUPER GAMING X TRIO White", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI_S,	MSIGPUIDs.RTX4070TI_GAMING_X_SLIM,		0x68, "MSI 4070Ti SUPER GAMING X SLIM", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4070TI_S,	MSIGPUIDs.RTX4070TI_S_GAMING_X_SLIM,	0x68, "MSI 4070Ti SUPER GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI_S,	MSIGPUIDs.RTX4070TI_S_GAMING_X_SLIM_W,	0x68, "MSI 4070Ti SUPER GAMING X SLIM White", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI_S,	MSIGPUIDs.RTX4070TI_S_GAMING_X_SLIM_W_2,	0x68, "MSI 4070Ti SUPER GAMING X SLIM White", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4070TI_S,	MSIGPUIDs.RTX4070TI_S_GAMING_X_SLIM_STALKER_2,	0x68, "MSI 4070Ti SUPER Stalker 2", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4080,		MSIGPUIDs.RTX4080_SUPRIM_X,				0x68, "MSI 4080 SUPRIM X", "SUPRIM X"),
        	new MSIGPUIdentifier(Nvidia.RTX4080,		MSIGPUIDs.RTX4080_GAMING_X_TRIO,		0x68, "MSI 4080 GAMING X TRIO", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4080,		MSIGPUIDs.RTX4080_GAMING_X_TRIO_W,		0x68, "MSI 4080 GAMING X TRIO White", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4080,		MSIGPUIDs.RTX4080_GAMING_X_SLIM_W,		0x68, "MSI 4080 GAMING X SLIM White", "GAMING X"),

        	new MSIGPUIdentifier(Nvidia.RTX4080_S,		MSIGPUIDs.RTX4080_S_GAMING_X_SLIM,		0x68, "MSI 4080 SUPER GAMING X SLIM", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4080_S,		MSIGPUIDs.RTX4080_GAMING_X_SLIM_W,		0x68, "MSI 4080 SUPER GAMING X SLIM White", "GAMING X"),
        	new MSIGPUIdentifier(Nvidia.RTX4080_S,		MSIGPUIDs.RTX4080_SUPRIM_X,				0x68, "MSI 4080 SUPER SUPRIM X", "SUPRIM X"), // Same PID as the normal 4080
        	new MSIGPUIdentifier(Nvidia.RTX4080_S,		MSIGPUIDs.RTX4080_GAMING_X_TRIO,		0x68, "MSI 4080 SUPER GAMING X TRIO", "GAMING TRIO"),

        	new MSIGPUIdentifier(Nvidia.RTX4090,		MSIGPUIDs.RTX4090_GAMING_TRIO,			0x68, "MSI 4090 GAMING TRIO", "GAMING TRIO"),
        	new MSIGPUIdentifier(Nvidia.RTX4090,		MSIGPUIDs.RTX4090_SUPRIM_LIQUID_X,		0x68, "MSI 4090 SUPRIM LIQUID X", "SUPRIM LIQUID X"),
        	new MSIGPUIdentifier(Nvidia.RTX4090,		MSIGPUIDs.RTX4090_SUPRRIM_X,			0x68, "MSI 4090 SUPRIM X", "SUPRIM X"),
        	new MSIGPUIdentifier(Nvidia.RTX4090,		MSIGPUIDs.RTX4090_SUPRIM_X_CLASSIC,		0x68, "MSI 4090 SUPRIM X Classic", "SUPRIM X"),
        	new MSIGPUIdentifier(Nvidia.RTX4090,		MSIGPUIDs.RTX4090_SUPRIM_X_CLASSIC_2,	0x68, "MSI 4090 SUPRIM X Classic", "SUPRIM X"),
        	new MSIGPUIdentifier(Nvidia.RTX4090,		MSIGPUIDs.RTX4090_GAMING_X_SLIM,		0x68, "MSI 4090 GAMING X SLIM", "GAMING X")
        ];
	}
}

const MSILEDLibrary = {
	"GAMING X": {
		size: [3, 1],
		leds: [3, 4, 5],
		ledsPositions: [[0, 0], [1, 0], [2, 0]],
		ledsNames: ["LED 1", "LED 2", "LED 3"],
	},
	"GAMING TRIO": {
		size: [10, 10],
		leds: [
			0, 1, 2, 					3,
									 4,
								 5,


						 6,
					 7,
				 8,
		],
		ledsPositions: [
			[0, 0], [1, 0], [2, 0], 					[9, 0],
													 [8, 1],
												 [7, 2],


										 [5, 7],
									 [4, 8],
								 [3, 9],
		],
		ledsNames: ["LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9"],
	},
	"SUPRIM X": { // Fan V strips are mirrored
		size: [16, 12],
		leds: [
			0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,

				 20,
					 21,
						 22,
					 		23,
								 24, 				30, 31,
								 25,				32, 33,
							 26,
						 27,
					 28,
				 29
		],
		ledsPositions: [
			[0, 0], [1, 0], [2, 0], [3, 0], [4, 0], [5, 0], [6, 0], [7, 0], [8, 0], [9, 0], [10, 0], [11, 0], [12, 0], [13, 0], [14, 0], [15, 0],

					 [5, 2],
						 [6, 3],
							 [7, 4],
							 	[8, 5],
									 [9, 6], 				[12, 6], [13, 6],
									 [9, 7],				[12, 7], [13, 7],
							 	[8, 8],
						 	 [7, 9],
					 	 [6, 10],
				 	 [5, 11]
		],
		ledsNames: [
			"LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12", "LED 13", "LED 14", "LED 15", "LED 16",
			"LED 17", "LED 18", "LED 19", "LED 20", "LED 21", "LED 22", "LED 23", "LED 24", "LED 25", "LED 26",	"LED 27", "LED 28", "LED 29", "LED 30"],
	},
	"SUPRIM LIQUID X": {
		size: [5, 8],
		leds: [
				 0,
					 1,
						 2,
			30, 31, 		3,
			32, 33, 		4,
						 5,
					 6,
				 7
		],
		ledsPositions: [
				 [1, 0],
					 	[2, 1],
						 	[3, 2],
			[0, 3], [1, 3], 		[4, 3],
			[0, 4], [1, 4], 		[4, 4],
						 	[3, 5],
					 	[2, 6],
				 [1, 7]
		],
		ledsNames: ["LED 1", "LED 2", "LED 3", "LED 4", "LED 5", "LED 6", "LED 7", "LED 8", "LED 9", "LED 10", "LED 11", "LED 12"],
	}
};

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

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
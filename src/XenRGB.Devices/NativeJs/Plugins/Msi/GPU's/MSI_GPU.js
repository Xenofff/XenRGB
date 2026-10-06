import {Assert} from "@SignalRGB/Errors.js";
import permissions from "@SignalRGB/permissions";

export function Name() { return "MSI GPU"; }
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
*/
export function ControllableParameters() {
	return [
		{"property":"shutdownColor", "group":"lighting", "label":"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", "min":"0", "max":"360", "type":"color", "default":"#000000"},
		{"property":"LightingMode", "group":"lighting", "label":"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", "type":"combobox", "values":["Canvas", "Forced"], "default":"Canvas"},
		{"property":"forcedColor", "group":"lighting", "label":"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", "min":"0", "max":"360", "type":"color", "default":"#009bde"},
	];
}

export function DeviceMessages() {
	return [
		{property: "Single Zone Control", message:"GPU is Limited to a Single Zone.", tooltip: "This device's firmware is limited to a single rgb zone while controlled directly."},
	];
}

const vLedNames = [ "GPU" ];
const vLedPositions = [ [0, 0] ];

let startupRed;
let startupBlue;
let startupGreen;
let startupBrightness;
let startupMode;
let is40SeriesCard = false;
let is50SeriesCard = false;

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
		initializeGPU();
	}
}

export function Initialize() {
	SetGPUNameFromBusIds(new MSIGPUList().devices);

	console.log("Is 40 Series Card: " + is40SeriesCard);
	console.log("Is 50 Series Card: " + is50SeriesCard);

	permissionManager.Register();
	
	if(permissionManager.GetPermission("lighting")) {
		initializeGPU();
	}
}

export function Render() {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	sendColors();
}

export function Shutdown(SystemSuspending) {
	if(!permissionManager.GetPermission("lighting")){
		return;
	}

	const color = SystemSuspending ? "#000000" : shutdownColor;
	sendColors(color);
}

function initializeGPU() {
	CheckedWrite(0x2E, 0x00);

	if(is40SeriesCard){
		CheckedWrite(0x2D, 0x00); //40 Series flags
	}

	if(is50SeriesCard){
		CheckedWrite(0x46, 0x01); //50 Series flags
	}

	MSIGPU.setDeviceMode(MSIGPU.modes.STATIC);
	MSIGPU.setDeviceBrightness(0x64);

	if(is40SeriesCard){
		MSIGPU.setDeviceEffectSpeed(0x00); //Yay 40 series quirks
	}

	if(is50SeriesCard){
		MSIGPU.setDeviceEffectSpeed(0x00); //Yay 50 series quirks
	}
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

function SetGPUNameFromBusIds(GPUList) {
	for(const GPU of GPUList) {
		if(CheckForIdMatch(bus, GPU)) {

			// We could do a product id check here, or a subdevice id check, but I'm not sure if that's necessary.
			// This may need to change with 5000 series cards in the future too.
			is40SeriesCard = GPU.Name.startsWith("MSI 40");
			is50SeriesCard = GPU.Name.startsWith("MSI 50");
			device.setName(GPU.Name);

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

//const PreviousColors = [0, 0, 0];

function sendColors(overrideColor) {
	let color;

	if(overrideColor) {
		color = hexToRgb(overrideColor);
	} else if (LightingMode === "Forced") {
		color = hexToRgb(forcedColor);
	} else {
		color = device.color(vLedPositions[0][0],  vLedPositions[0][1]);
	}

	if(is40SeriesCard){
		MSIGPU.setDeviceMode(MSIGPU.modes.STATIC);
	}

	if(is50SeriesCard){
		MSIGPU.setDeviceMode(MSIGPU.modes.STATIC);
	}

	if(is50SeriesCard){
		CheckedWrite(0x46, 0x01);
	}

	//We aren't using the start flag here. The 40 series card didn't need it, so let's just try without.
	//if(PreviousColors[0] !== color[0]) {
	CheckedWrite(MSIGPU.registers.R1, color[0]);
	//	PreviousColors[0] = color[0];
	//}

	//if(PreviousColors[1] !== color[1]) {
	CheckedWrite(MSIGPU.registers.G1, color[1]);
	//	PreviousColors[1] = color[1];
	//}

	//if(PreviousColors[2] !== color[2]) {
	CheckedWrite(MSIGPU.registers.B1, color[2]);
	//	PreviousColors[2] = color[2];
	//}

	device.pause(120);
}

class MSIGPUController {
	constructor() {
		this.registers =
        {
        	BRIGHTNESS                 : 0x36,
        	SPEED                      : 0x38,
        	START                      : 0x26,
        	R1                         : 0x30,
        	G1                         : 0x31,
        	B1                         : 0x32,
        	R2                         : 0x27,
        	G2                         : 0x28,
        	B2                         : 0x29,
        	R3                         : 0x2a,
        	G3                         : 0x2b,
        	B3                         : 0x2c,
        	MODE                       : 0x22,
        	APPLY                      : 0x3f,
        	MSI_GPU_APPLY_VAL          : 0x01,
        };

		this.commands =
        {
        	action    : 0x80,
        	speed     : 0x20,
        	direction : 0x24,
        	apply     : 0x2F
        };

		this.modes =
        {
        	OFF                       : 0x01,
        	RAINBOW                   : 0x08,
        	STATIC                    : 0x13,
        	RAINDROP                  : 0x1a,
        	MAGIC                     : 0x07,
        	PATROLLING                : 0x05,
        	STREAMING                 : 0x06,
        	LIGHTNING                 : 0x15,
        	WAVE                      : 0x1f,
        	METEOR                    : 0x16,
        	MARQUEE                   : 0x18,
        	STACK                     : 0x0d,
        	RHYTHM                    : 0x0b,
        	FLOWING                   : 0x09,
        	WHIRLING                  : 0x0f,
        	TWISTING                  : 0x11,
        	LAMINATING                : 0x1d,
        	FADEIN                    : 0x14,
        	BREATHING                 : 0x04,
        	FLASHING                  : 0x02,
        	DOUBLEFLASHING            : 0x03,
        };
	}

	getStartupValues() //Return startup color and brightness values.
	{
		const startupRed = bus.ReadByte(this.registers.R1);
		const startupGreen = bus.ReadByte(this.registers.G1);
		const startupBlue = bus.ReadByte(this.registers.B1);
		const startupBrightness = bus.ReadByte(this.registers.BRIGHTNESS);
		const startupMode = bus.ReadByte(this.registers.MODE);

		return [startupRed, startupBlue, startupGreen, startupBrightness, startupMode];
	}

	initializeGPU(brightness, mode) {
		if(mode !== this.modes.STATIC) {
			this.setDeviceMode(this.modes.STATIC);
			device.log(this.getStartupValues[4]); //Recheck
		}

		if(brightness !== 0x64) {
			this.setDeviceBrightness(0x64);
			device.log(this.getStartupValues[3]); //Recheck brightness
		}

		device.log("Startup Color Code" + (startupRed << 8) + (startupGreen << 8) + (startupBlue << 8));
	}

	setDeviceMode(mode) {
		CheckedWrite(this.registers.MODE, mode);
	}

	setDeviceBrightness(brightness) {
		CheckedWrite(this.registers.BRIGHTNESS, brightness);
	}

	setDeviceEffectSpeed(speed) {
		CheckedWrite(this.registers.SPEED, speed);
	}
}

const MSIGPU = new MSIGPUController();

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
	constructor(device, SubDevice, Address, Name) {
		super(0x10DE, 0x1462, device, SubDevice, Address, Name, "");
	}
}

class NvidiaGPUDeviceIds {
	constructor() {
		this.GTX1050TI       = 0x1C82;
		this.GTX1060         = 0x1C03;
		this.GTX1070         = 0x1B81;
		this.GTX1070TI       = 0x1B82;
		this.GTX1080         = 0x1B80;
		this.GTX1080TI       = 0x1B06;
		this.GTX1650         = 0x1F82;
		this.GTX1650S        = 0x2187;
		this.GTX1660         = 0x2184;
		this.GTX1660TI       = 0x2182;
		this.GTX1660S        = 0x21C4;
		this.RTX2060_TU104   = 0x1E89;
		this.RTX2060_TU106   = 0x1F08;
		this.RTX2060S        = 0x1F47;
		this.RTX2060S_OC     = 0x1F06;
		this.RTX2070         = 0x1F02;
		this.RTX2070_OC      = 0x1F07;
		this.RTX2070S        = 0x1E84;
		this.RTX2080         = 0x1E82;
		this.RTX2080_A       = 0x1E87;
		this.RTX2080S        = 0x1E81;
		this.RTX2080TI_TU102 = 0x1E04;
		this.RTX2080TI       = 0x1E07;
		this.RTX2080_SUPER   = 0x1E81;
		this.RTX3050         = 0x2507;
		this.RTX3060         = 0x2503;
		this.RTX3060_LHR     = 0x2504;
		this.RTX3060_GA104   = 0x2487;
		this.RTX3060TI       = 0x2486;
		this.RTX3060TI_LHR   = 0x2489;
		this.RTX3060TI_GDDR6X = 0x24C9;
		this.RTX3070         = 0x2484;
		this.RTX3070_LHR     = 0x2488;
		this.RTX3070TI       = 0x2482;
		this.RTX3080         = 0x2206;
		this.RTX3080_LHR     = 0x2216;
		this.RTX3080_GA102   = 0x220A;
		this.RTX3080TI       = 0x2208;
		this.RTX3090         = 0x2204;
		this.RTX3090TI       = 0x2203;
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
		this.RTX5070         = 0x2F04;
		this.RTX5070TI		 = 0x2C05;
		this.RTX5080		 = 0x2C02;
		this.RTX5090		 = 0x2B85;
	}
};

class MSIGPUDeviceIDs {
	constructor() {
		// 1000 Series
		this.MSI_GTX1060_3GB                         = 0x3285;
		this.MSI_GTX1060_6GB                         = 0x3282;
		//MSI_GTX1070TI_TITANIUM                          0xc300 //FAILED
		this.MSI_GTX1070_GAMING_X                    = 0x3306;
		this.MSI_GTX1070_GAMING_X_8G                 = 0x3302;
		// MSI_GTX1080_GAMING_X_PLUS                       0x3362 //FAILED
		//MSI_GTX1080TI_GAMING_X                          0x3602 //FAILED
		this.MSI_GTX1080TI_GAMING_X                  = 0x3603;
		this.MSI_GTX1080TI_LIGHTNING_Z               = 0x3660;
		this.MSI_GTX1080_DUKE                        = 0x3369;

		// 1660 Series
		this.MSI_GTX1660_GAMING_X_6G                 = 0x3790;
		this.MSI_GTX1660TI_GAMING_X_6G               = 0x375A;
		this.MSI_GTX1660TI_GAMING_X_6G_2             = 0x375C;
		this.MSI_GTX1660_SUPER_GAMING_6G             = 0xC759;
		this.MSI_GTX1660_SUPER_GAMING_X_6G           = 0xC758;
		this.MSI_GTX1660S_VENTUS_XS_OC               = 0xC75A;

		// 2000 Series
		this.MSI_RTX2060_GAMING_Z_6G                 = 0x3752;
		this.MSI_RTX2060_GAMING_Z_6G_2               = 0x3754;
		this.MSI_RTX2060_SUPER_GAMING_X              = 0xC752;
		this.MSI_RTX2060_SUPER_GAMING                = 0xC753;
		this.MSI_RTX2060_SUPER_ARMOR_OC              = 0xC754;
		this.MSI_RTX2070_GAMING_Z_SUB_DEV            = 0x3732;
		this.MSI_RTX2070_GAMING                      = 0x3733;
		this.MSI_RTX2070_ARMOR                       = 0x3734;
		this.MSI_RTX2070_SUPER_GAMING_TRIO           = 0xC727;
		this.MSI_RTX2070_SUPER_GAMING_Z_TRIO         = 0x37B6;
		this.MSI_RTX2070_SUPER_GAMING_X              = 0x373e;
		this.MSI_RTX2070_SUPER_GAMING_X_TRIO         = 0xC726;
		this.MSI_RTX2080_SUPER_SEA_HAWK_EK_X		 = 0xC72E;
		this.MSI_RTX2080_DUKE_OC                     = 0x3721;
		this.MSI_RTX2080_GAMING_TRIO                 = 0x372E;
		this.MSI_RTX2080_GAMING_X_TRIO               = 0x3726;
		this.MSI_RTX2080_SEA_HAWK_EK_X               = 0x3728;
		this.MSI_RTX2080S_GAMING_X_TRIO              = 0xC724;
		this.MSI_RTX2080S_GAMING_X_TRIO_2			 = 0xC725;
		this.MSI_RTX2080TI_GAMING_X_TRIO             = 0x3715;
		this.MSI_RTX2080TI_GAMING_Z_TRIO             = 0x371E;
		this.MSI_RTX2080TI_SEA_HAWK_EK_X             = 0x3717;
		this.MSI_RTX2080TI_LIGHTNING_Z               = 0x3770;
		this.MSI_RTX2080TI_DUKE_OC					 = 0x3710;

		// 3000 Series
		this.MSI_RTX3050_GAMING_X_8G				 = 0xC979;

		this.MSI_RTX3060_GAMING_X_12G                = 0x3976;
		this.MSI_RTX3060_GAMING_X_TRIO_12G           = 0x3903;
		this.MSI_RTX3060_GAMING_X_TRIO_LHR           = 0x3903;
		this.MSI_RTX3060TI_GAMING_X_LHR              = 0x3973;
		this.MSI_RTX3060TI_GAMING_X_TRIO_LHR         = 0x3903;
		this.MSI_RTX3060TI_SUPER_3X					 = 0x505A;
		this.MSI_RTX3060TI_GAMING_X_TRIO_LHR_GDDR6X	 = 0x5058;

		this.MSI_RTX3070_SUPRIM_X                    = 0x3901;
		this.MSI_RTX3070_SUPRIM_X_2					 = 0x3902;
		this.MSI_RTX3070_SUPRIM                      = 0x390C;
		this.MSI_RTX3070_GAMING_X_TRIO               = 0x3903;
		this.MSI_RTX3070_GAMING_Z_TRIO               = 0x3904;
		this.MSI_RTX3070TI_SUPRIM_X                  = 0x5051;
		this.MSI_RTX3070TI_GAMING_X_TRIO             = 0x5052;

		this.MSI_RTX3080_GAMING_TRIO				 = 0x3893;
		this.MSI_RTX3080_GAMING_X_TRIO               = 0x3892;
		this.MSI_RTX3080_GAMING_Z_TRIO               = 0x389B;
		this.MSI_RTX3080_SUPRIM						 = 0x3895;

		this.MSI_RTX3080_SUPRIM_X                    = 0x3897;
		this.MSI_RTX3080TI_GAMING_X_TRIO             = 0x389B;
		this.MSI_RTX3090_GAMING_X_TRIO               = 0x3884;
		this.MSI_RTX3090_GAMING_X_TRIO_2			 = 0x3885;
		this.MSI_RTX3090_SUPRIM_X                    = 0x3882;
		this.MSI_RTX3090TI_SUPRIX_X                  = 0x5090;
		this.MSI_RTX3090TI_GAMING_TRIO               = 0x5091;

		this.RTX5080_SUPRIM_LIQUID_SOC				 = 0x5312;

		this.RTX5090_SUPRIM_LIQUID_SOC				 = 0x5300;
	}
}

class MSIGPUList {
	constructor() {
		const Nvidia = new NvidiaGPUDeviceIds();
		const MSIGPUIDs  = new MSIGPUDeviceIDs();
		this.devices =
        [

        	new MSIGPUIdentifier(Nvidia.GTX1060, 			MSIGPUIDs.MSI_GTX1060_3GB,							0x68, "MSI 1060 Gaming 3GB"), //Untested
        	new MSIGPUIdentifier(Nvidia.GTX1060, 			MSIGPUIDs.MSI_GTX1060_6GB,							0x68, "MSI 1060 Gaming 6GB"), //Untested
        	new MSIGPUIdentifier(Nvidia.GTX1070, 			MSIGPUIDs.MSI_GTX1070_GAMING_X,						0x68, "MSI 1070 Gaming X"),
        	new MSIGPUIdentifier(Nvidia.GTX1070, 			MSIGPUIDs.MSI_GTX1070_GAMING_X_8G,					0x68, "MSI 1070 Gaming X 8G"), //Untested
        	// new MSIGPUIdentifier(Nvidia.GTX1080, 			MSIGPUIDs.MSI_GTX1080_DUKE,			0x68, "MSI 1080 DUKE"), //Untested, and unlikely to work, based on nvapi capture provided
        	// new MSIGPUIdentifier(Nvidia.GTX1080TI, 			MSIGPUIDs.MSI_GTX1080TI_GAMING_X,			0x68, "MSI 1080 Gaming X"), //Untested and unlikely to work. Dev is 0x38.

        	new MSIGPUIdentifier(Nvidia.GTX1080TI, 			MSIGPUIDs.MSI_GTX1080TI_LIGHTNING_Z,			    0x68, "MSI 1080 Lightning Z"),
        	new MSIGPUIdentifier(Nvidia.GTX1660, 			MSIGPUIDs.MSI_GTX1660_GAMING_X_6G,					0x68, "MSI 1660 Gaming X 6G"),
        	new MSIGPUIdentifier(Nvidia.GTX1660TI, 			MSIGPUIDs.MSI_GTX1660TI_GAMING_X_6G,				0x68, "MSI 1060Ti Gaming X"),
        	new MSIGPUIdentifier(Nvidia.GTX1660TI, 			MSIGPUIDs.MSI_GTX1660TI_GAMING_X_6G_2,				0x68, "MSI 1060Ti Gaming X"), //Untested
        	new MSIGPUIdentifier(Nvidia.GTX1660S, 			MSIGPUIDs.MSI_GTX1660_SUPER_GAMING_6G,				0x68, "MSI 1660 Super Gaming 6G"),
        	new MSIGPUIdentifier(Nvidia.GTX1660S, 			MSIGPUIDs.MSI_GTX1660_SUPER_GAMING_X_6G,			0x68, "MSI 1660 Super Gaming X 6G"),
        	new MSIGPUIdentifier(Nvidia.GTX1660S, 			MSIGPUIDs.MSI_GTX1660S_VENTUS_XS_OC,				0x68, "MSI 1660 Super Ventos XS OC"),

        	new MSIGPUIdentifier(Nvidia.RTX2060_TU104,		MSIGPUIDs.MSI_RTX2060_GAMING_Z_6G,					0x68, "MSI 2060 Gaming Z"),
        	new MSIGPUIdentifier(Nvidia.RTX2060_TU106,		MSIGPUIDs.MSI_RTX2060_GAMING_Z_6G,					0x68, "MSI 2060 Gaming Z"),
        	new MSIGPUIdentifier(Nvidia.RTX2060_TU106,		MSIGPUIDs.MSI_RTX2060_GAMING_Z_6G_2,				0x68, "MSI 2060 Gaming Z"),
        	new MSIGPUIdentifier(Nvidia.RTX2060S_OC,		MSIGPUIDs.MSI_RTX2060_SUPER_GAMING_X,				0x68, "MSI 2060 Super Gaming X"),
        	new MSIGPUIdentifier(Nvidia.RTX2060S_OC,		MSIGPUIDs.MSI_RTX2060_SUPER_GAMING,					0x68, "MSI 2060 Super Gaming"),
        	new MSIGPUIdentifier(Nvidia.RTX2060S_OC,		MSIGPUIDs.MSI_RTX2060_SUPER_ARMOR_OC,				0x68, "MSI 2060 Super Armor OC"),
        	new MSIGPUIdentifier(Nvidia.RTX2070_OC,			MSIGPUIDs.MSI_RTX2070_GAMING_Z_SUB_DEV,				0x68, "MSI 2070 Gaming Z"),
        	new MSIGPUIdentifier(Nvidia.RTX2070_OC, 		MSIGPUIDs.MSI_RTX2070_GAMING,					    0x68, "MSI 2070 Gaming Z"),
        	new MSIGPUIdentifier(Nvidia.RTX2070, 			MSIGPUIDs.MSI_RTX2070_GAMING,						0x68, "MSI 2070 Gaming"),
        	new MSIGPUIdentifier(Nvidia.RTX2070, 			MSIGPUIDs.MSI_RTX2070_ARMOR,						0x68, "MSI 2070 Armor"),
        	new MSIGPUIdentifier(Nvidia.RTX2070_OC,			MSIGPUIDs.MSI_RTX2070_ARMOR,						0x68, "MSI 2070 Armor OC"),
        	new MSIGPUIdentifier(Nvidia.RTX2070S, 			MSIGPUIDs.MSI_RTX2070_SUPER_GAMING_TRIO,			0x68, "MSI 2070 Super Gaming Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2070S, 			MSIGPUIDs.MSI_RTX2070_SUPER_GAMING_X,				0x68, "MSI 2070 Super Gaming X"),
        	new MSIGPUIdentifier(Nvidia.RTX2070S, 			MSIGPUIDs.MSI_RTX2070_SUPER_GAMING_X_TRIO,			0x68, "MSI 2070 Super Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2070S, 			MSIGPUIDs.MSI_RTX2070_SUPER_GAMING_Z_TRIO,			0x68, "MSI 2070 Super Gaming Z Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2080, 			MSIGPUIDs.MSI_RTX2080_GAMING_TRIO,					0x68, "MSI 2080 Gaming Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2080_A, 			MSIGPUIDs.MSI_RTX2080_GAMING_X_TRIO,				0x68, "MSI 2080 Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2080_A, 			MSIGPUIDs.MSI_RTX2080_DUKE_OC,						0x68, "MSI 2080 Duke OC"),
        	new MSIGPUIdentifier(Nvidia.RTX2080_A, 			MSIGPUIDs.MSI_RTX2080_SEA_HAWK_EK_X,				0x68, "MSI 2080 Sea Hawk EK x"),
        	new MSIGPUIdentifier(Nvidia.RTX2080S, 			MSIGPUIDs.MSI_RTX2080S_GAMING_X_TRIO,				0x68, "MSI 2080 Super Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2080S, 			MSIGPUIDs.MSI_RTX2080S_GAMING_X_TRIO_2,				0x68, "MSI 2080 Super Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2080S,			MSIGPUIDs.MSI_RTX2080_SUPER_SEA_HAWK_EK_X,			0x68, "MSI 2080 Super SEA HAWK EK"),
        	new MSIGPUIdentifier(Nvidia.RTX2080TI, 			MSIGPUIDs.MSI_RTX2080TI_GAMING_X_TRIO,				0x68, "MSI 2080Ti Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX2080TI, 			MSIGPUIDs.MSI_RTX2080TI_GAMING_Z_TRIO,				0x68, "MSI 2080Ti Gaming Z Trio"), //Untested
        	new MSIGPUIdentifier(Nvidia.RTX2080TI, 			MSIGPUIDs.MSI_RTX2080TI_SEA_HAWK_EK_X,				0x68, "MSI 2080Ti Sea Hawk EK X"),
        	new MSIGPUIdentifier(Nvidia.RTX2080TI, 			MSIGPUIDs.MSI_RTX2080TI_LIGHTNING_Z,				0x68, "MSI 2080Ti Lightning Z"),    //Untested
        	new MSIGPUIdentifier(Nvidia.RTX2080TI, 			MSIGPUIDs.MSI_RTX2080TI_DUKE_OC,					0x68, "MSI 2080Ti DUKE OC"),

        	new MSIGPUIdentifier(Nvidia.RTX3050, 			MSIGPUIDs.MSI_RTX3050_GAMING_X_8G,					0x68, "MSI 3050 Gaming X 8G"),

        	new MSIGPUIdentifier(Nvidia.RTX3060, 			MSIGPUIDs.MSI_RTX3060_GAMING_X_TRIO_12G,			0x68, "MSI 3060 Gaming X Trio 12G"),     //Duplicate sub dev id on different dev id
        	new MSIGPUIdentifier(Nvidia.RTX3060_GA104,		MSIGPUIDs.MSI_RTX3060_GAMING_X_TRIO_12G,			0x68, "MSI 3060 Gaming X Trio 12G"),     //Duplicate sub dev id on different dev id
        	new MSIGPUIdentifier(Nvidia.RTX3060, 			MSIGPUIDs.MSI_RTX3060_GAMING_X_12G,					0x68, "MSI 3060 Gaming X 12g"),
        	new MSIGPUIdentifier(Nvidia.RTX3060_LHR,		MSIGPUIDs.MSI_RTX3060_GAMING_X_12G,					0x68, "MSI 3060 Gaming X 12G LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3060_GA104,		MSIGPUIDs.MSI_RTX3060_GAMING_X_12G,					0x68, "MSI 3060 Gaming X 12G (GA104)"),
        	new MSIGPUIdentifier(Nvidia.RTX3060_LHR,		MSIGPUIDs.MSI_RTX3060_GAMING_X_TRIO_LHR,			0x68, "MSI 3060 Gaming X Trio 12G LHR"),

        	new MSIGPUIdentifier(Nvidia.RTX3060TI, 			MSIGPUIDs.MSI_RTX3070_GAMING_X_TRIO,				0x68, "MSI 3060Ti Gaming X Trio"), //Duplicate sub dev id on different dev id
        	new MSIGPUIdentifier(Nvidia.RTX3060TI_LHR,		MSIGPUIDs.MSI_RTX3060TI_GAMING_X_LHR,				0x68, "MSI 3060Ti Gaming X LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3060TI_LHR,		MSIGPUIDs.MSI_RTX3060TI_GAMING_X_TRIO_LHR,			0x68, "MSI 3060Ti Gaming X Trio LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3060TI_GDDR6X,	MSIGPUIDs.MSI_RTX3060TI_GAMING_X_TRIO_LHR_GDDR6X,   0x68, "MSI 3060Ti Gaming X Trio LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3060TI_GDDR6X,	MSIGPUIDs.MSI_RTX3060TI_SUPER_3X,					0x68, "MSI 3060Ti Super 3X"),

        	new MSIGPUIdentifier(Nvidia.RTX3070, 			MSIGPUIDs.MSI_RTX3070_GAMING_X_TRIO,				0x68, "MSI 3070 Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3070, 			MSIGPUIDs.MSI_RTX3070_GAMING_Z_TRIO,				0x68, "MSI 3070 Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3070_LHR,		MSIGPUIDs.MSI_RTX3070_GAMING_Z_TRIO,				0x68, "MSI 3070 Gaming Z Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3070, 			MSIGPUIDs.MSI_RTX3070_SUPRIM_X,						0x68, "MSI 3070 Suprim X"),
			new MSIGPUIdentifier(Nvidia.RTX3070, 			MSIGPUIDs.MSI_RTX3070_SUPRIM_X_2,						0x68, "MSI 3070 Suprim X"),
        	new MSIGPUIdentifier(Nvidia.RTX3070_LHR,		MSIGPUIDs.MSI_RTX3070_SUPRIM_X,					0x68, "MSI 3070 Suprim X LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3070, 			MSIGPUIDs.MSI_RTX3070_SUPRIM,						0x68, "MSI 3070 Suprim"),
        	new MSIGPUIdentifier(Nvidia.RTX3070_LHR,		MSIGPUIDs.MSI_RTX3070_SUPRIM,						0x68, "MSI 3070 Suprim LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3070TI, 			MSIGPUIDs.MSI_RTX3070TI_GAMING_X_TRIO,				0x68, "MSI 3070Ti Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3070TI, 			MSIGPUIDs.MSI_RTX3070TI_SUPRIM_X,					0x68, "MSI 3070Ti Suprim X"),

        	new MSIGPUIdentifier(Nvidia.RTX3080, 			MSIGPUIDs.MSI_RTX3080_SUPRIM,						0x68, "MSI 3080 Suprim"),
        	new MSIGPUIdentifier(Nvidia.RTX3080, 			MSIGPUIDs.MSI_RTX3080_GAMING_TRIO,					0x68, "MSI 3080 Gaming Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3080, 			MSIGPUIDs.MSI_RTX3080_GAMING_X_TRIO,				0x68, "MSI 3080 Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3080_LHR,		MSIGPUIDs.MSI_RTX3080_GAMING_Z_TRIO,				0x68, "MSI 3080 Gaming Z Trio LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3080_GA102,		MSIGPUIDs.MSI_RTX3080_GAMING_Z_TRIO,				0x68, "MSI 3080 Gaming Z Trio 12g LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3080, 			MSIGPUIDs.MSI_RTX3080_SUPRIM_X,						0x68, "MSI 3080 Suprim X"),
        	new MSIGPUIdentifier(Nvidia.RTX3080_GA102,		MSIGPUIDs.MSI_RTX3080_SUPRIM_X,						0x68, "MSI 3080 Suprim X 12g"),
        	new MSIGPUIdentifier(Nvidia.RTX3080_LHR,		MSIGPUIDs.MSI_RTX3080_SUPRIM_X,						0x68, "MSI 3080 Suprim X LHR"),
        	new MSIGPUIdentifier(Nvidia.RTX3080, 			MSIGPUIDs.MSI_RTX3080TI_GAMING_X_TRIO,				0x68, "MSI 3080 Suprim X"),
        	new MSIGPUIdentifier(Nvidia.RTX3080TI, 			MSIGPUIDs.MSI_RTX3080TI_GAMING_X_TRIO,				0x68, "MSI 3080Ti Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3080TI, 			MSIGPUIDs.MSI_RTX3080_SUPRIM_X,						0x68, "MSI 3080Ti Suprim X"),
        	new MSIGPUIdentifier(Nvidia.RTX3090, 			MSIGPUIDs.MSI_RTX3090_GAMING_X_TRIO,				0x68, "MSI 3090 Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3090, 			MSIGPUIDs.MSI_RTX3090_GAMING_X_TRIO_2,				0x68, "MSI 3090 Gaming X Trio"),
        	new MSIGPUIdentifier(Nvidia.RTX3090, 			MSIGPUIDs.MSI_RTX3090_SUPRIM_X,						0x68, "MSI 3090 Suprim X"),
        	new MSIGPUIdentifier(Nvidia.RTX3090TI, 			MSIGPUIDs.MSI_RTX3090TI_SUPRIX_X,					0x68, "MSI 3090 TI Suprim X"), //Untested
        	new MSIGPUIdentifier(Nvidia.RTX3090TI, 			MSIGPUIDs.MSI_RTX3090TI_GAMING_TRIO,				0x68, "MSI 3090 TI Gaming Trio"), //Untested

        	new MSIGPUIdentifier(Nvidia.RTX5080,			MSIGPUIDs.RTX5080_SUPRIM_LIQUID_SOC,				0x68, "MSI 5080 SUPRIM LIQUID SOC"),

        	new MSIGPUIdentifier(Nvidia.RTX5090,			MSIGPUIDs.RTX5090_SUPRIM_LIQUID_SOC,				0x68, "MSI 5090 SUPRIM LIQUID SOC"),
        ];
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
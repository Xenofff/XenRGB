export function Name() { return "HyperX Device"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return Object.keys(HYPERXdeviceLibrary.PIDLibrary); }
export function Publisher() { return "WhirlwindFx"; }
export function Documentation(){ return "troubleshooting/hyperx"; }
export function Size() { return [1, 1]; }
export function DeviceType(){return "mouse";}
export function ImageUrl() { return "https://assets.signalrgb.com/devices/default/misc/usb-drive-render.png"; }
export function Validate(endpoint) { return endpoint.interface === 1; }
/* global
shutdownColor:readonly
LightingMode:readonly
forcedColor:readonly
*/
export function ControllableParameters() {
	return [
		{property:"shutdownColor", group:"lighting", label:"Shutdown Color", description: "This color is applied to the device when the System, or SignalRGB is shutting down", min:"0", max:"360", type:"color", default:"#000000"},
		{property:"LightingMode", group:"lighting", label:"Lighting Mode", description: "Determines where the device's RGB comes from. Canvas will pull from the active Effect, while Forced will override it to a specific color", type:"combobox", values:["Canvas", "Forced"], default:"Canvas"},
		{property:"forcedColor", group:"lighting", label:"Forced Color", description: "The color used when 'Forced' Lighting Mode is enabled", min:"0", max:"360", type:"color", default:"#009bde"},
	];
}

export function Initialize() {
	HYPERX.Initialize();
}

export function Render() {
	HYPERX.sendColors();
}

export function Shutdown(SystemSuspending) {
	const color = SystemSuspending ? "#000000" : shutdownColor;
	HYPERX.sendColors(color);
}

export class HYPERX_Device_Protocol {
	constructor() {
		this.Config = {
			DeviceProductID: 0x0000,
			DeviceName: "HyperX Device",
			DeviceEndpoint: { "interface": 1, "usage": 0x0001, "usage_page": 0xFF01, "collection": 0x0005 },
			LedNames: [],
			LedPositions: [],
			Leds: [],
		};
	}

	getDeviceProperties(deviceID) { return HYPERXdeviceLibrary.PIDLibrary[deviceID];};

	getDeviceProductId() { return this.Config.DeviceProductID; }
	setDeviceProductId(productID) { this.Config.DeviceProductID = productID; }

	getDeviceName() { return this.Config.DeviceName; }
	setDeviceName(deviceName) { this.Config.DeviceName = deviceName; }

	getDeviceEndpoint() { return this.Config.DeviceEndpoint; }
	setDeviceEndpoint(deviceEndpoint) { this.Config.DeviceEndpoint = deviceEndpoint; }

	getLedNames() { return this.Config.LedNames; }
	setLedNames(ledNames) { this.Config.LedNames = ledNames; }

	getLedPositions() { return this.Config.LedPositions; }
	setLedPositions(ledPositions) { this.Config.LedPositions = ledPositions; }

	getLeds() { return this.Config.Leds; }
	setLeds(leds) { this.Config.Leds = leds; }

	getDeviceImage() { return this.Config.image; }
	setDeviceImage(image) { this.Config.image = image; }

	Initialize() {
		//Initializing vars
		this.setDeviceProductId(device.productId());

		const DeviceProperties = this.getDeviceProperties(this.getDeviceProductId());
		this.setDeviceName(DeviceProperties.name);
		this.setDeviceEndpoint(DeviceProperties.endpoint);
		this.setLedNames(DeviceProperties.LedNames);
		this.setLedPositions(DeviceProperties.LedPositions);
		this.setLeds(DeviceProperties.Leds);
		this.setDeviceImage(DeviceProperties.image);

		device.log("Device model found: " + this.getDeviceName());
		device.setName("HyperX " + this.getDeviceName());
		device.setSize(DeviceProperties.size);
		device.setControllableLeds(this.getLedNames(), this.getLedPositions());
		device.setImageFromUrl(this.getDeviceImage());
		device.set_endpoint(
			DeviceProperties.endpoint[`interface`],
			DeviceProperties.endpoint[`usage`],
			DeviceProperties.endpoint[`usage_page`],
			DeviceProperties.endpoint[`collection`]);

		console.log(`Developed on firmware v${DeviceProperties.firmware}`);
	}

	sendColors(overrideColor) {

		const deviceLedPositions	= this.getLedPositions();
		const deviceLeds			= this.getLeds();
		const RGBData				= [];

		for (let i = 0; i < deviceLeds.length; i++) {
			const iPxX = deviceLedPositions[i][0];
			const iPxY = deviceLedPositions[i][1];
			let color;

			if(overrideColor){
				color = hexToRgb(overrideColor);
			}else if (LightingMode === "Forced") {
				color = hexToRgb(forcedColor);
			}else {
				color = device.color(iPxX, iPxY);
			}

			RGBData[(deviceLeds[i]*3)]   = color[0];
			RGBData[(deviceLeds[i]*3)+1] = color[1];
			RGBData[(deviceLeds[i]*3)+2] = color[2];
		}

		device.send_report([0x07, 0x0A].concat(RGBData).concat([0x00, 0x00, 0x00, 0xA0]), 264);
		device.pause(1);
	}
}

export class deviceLibrary {
	constructor(){
		this.PIDLibrary	=	{
			0x16D7: {
				name: "PulseFire FPS Pro",
				size: [1, 1],
				LedNames: ["Scroll and Logo"],
				LedPositions: [[0, 0]],
				Leds: [0],
				endpoint: { "interface": 1, "usage": 0x0001, "usage_page": 0xFF01, "collection": 0x0005 },
				firmware: "1.1.2.3",
				image: "https://assets.signalrgb.com/devices/brands/hyperx/mice/pulsefire-fps-pro.png"
			},
		};
	}
}

const HYPERXdeviceLibrary = new deviceLibrary();
const HYPERX = new HYPERX_Device_Protocol();

function hexToRgb(hex) {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	const colors = [];
	colors[0] = parseInt(result[1], 16);
	colors[1] = parseInt(result[2], 16);
	colors[2] = parseInt(result[3], 16);

	return colors;
}

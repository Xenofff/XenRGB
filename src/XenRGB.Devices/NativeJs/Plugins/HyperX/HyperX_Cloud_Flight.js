export function Name() { return "HyperX Cloud Flight"; }
export function VendorId() { return 0x0951; }
export function ProductId() { return [0x16C4, 0x1723]; } // Developed with 0x16C4 on hand. Have not tested with 0x1723, but should work the same
export function Publisher() { return "Derek Huber"; }
export function Type() { return "HID"; }
export function Size() { return [1, 1]; }
export function DefaultPosition() { return [0, 0]; }
export function DefaultScale() { return 1.0; }
export function LacksOnBoardLeds() { return true; }
export function DeviceType(){return "headphones"}
/* global
batteryPollControl:readonly
*/
export function ControllableParameters() {
	return [
		{"property":"batteryPollControl", "label":"Battery Poll Rate (Seconds)", description: "Sets how often we read the battery level of this device", "step":"1",  "type":"number", "min":"1", "max":"20",  "default":"15"}
	];
}

// USB Report Ids
const HeadsetStatusReportId = 0xFF;
const BatteryReportId = 0x21;
const MicrophoneReportId = 0x00;

// Power state
const HeadsetOnOffIndex = 1;
const HeadsetOnVal = 0x0C;
const HeadsetOffVal = 0x01;

// Charging state
const BatteryChargingIndex = 11;
const ChargingVal = 0x05;
const NotChargingVal = 0x03;

// Microphone state
const MicrophoneIndex = 1;
const MicrophoneMutedVal = 0x04;
const MicrophoneUnmutedVal = 0x00;

const Headset = {
	headsetOn: null,
	headsetCharging: null
};

let batteryPollInterval = 15000; // Milliseconds - Default of 15 seconds
let savedPollTimer = Date.now();

const BatteryState = {
	Unknown: 0,
	Draining: 1,
	Charging: 2,
	ChargingFull: 3, // Unable to report this since battery % is estimated using voltage.  When charging, battery will read out as 100% regardless.
	Full: 4,
	WirelessCharging: 5 // This device doesn't support wireless charging
};

export function onbatteryPollControlChanged() {
	batteryPollInterval = batteryPollControl * 1000;
}

export function Validate(endpoint) {
	return endpoint.interface === 3;
	// Endpoints available...
	//device.set_endpoint(3, 0x0001, 0x000C, 0x0001); // Volume control (read)
	//device.set_endpoint(3, 0x0001, 0xFFC0, 0x0002); // Mic mute (read)
	//device.set_endpoint(3, 0x0303, 0xFF53, 0x0003); // Battery (write/read)
	//device.set_endpoint(3, 0x0001, 0xFF00, 0x0004); // Status (send/get report)
}

export function Initialize() {
	device.addFeature('battery');
	battery.setBatteryState(BatteryState.Unknown);
	onbatteryPollControlChanged();
}

export function Render() {

	GetHeadsetStatuses();
	PollBatteryStatus();
	CheckMicrophoneStatus();
	device.pause(1000);

}

export function Shutdown(SystemSuspending) {

}

// On/off + charging states
function GetHeadsetStatuses() {
	device.set_endpoint(3, 0x0001, 0xFF00, 0x0004);

	const sendPacket = [];
	sendPacket.push(0xFF, 0x09, 0x00, 0xFD, 0x04, 0x00, 0xF1, 0x05, 0x81, 0x74, 0xB4, 0x01);
	device.send_report(sendPacket, 64);

	const reportId = [HeadsetStatusReportId];
	let readPacket = [];
	readPacket = device.get_report(reportId, 64);

	// Accounts for null value
	if (Headset.headsetOn !== false && readPacket[HeadsetOnOffIndex] === HeadsetOffVal) {

		device.log('Headset is powered off...will be able to report statuses once powered back on.');
		Headset.headsetOn = false;
		Headset.headsetCharging = null;
		battery.setBatteryState(BatteryState.Unknown);

	} else if (!Headset.headsetOn && readPacket[HeadsetOnOffIndex] === HeadsetOnVal) {

		device.log('Headset is powered on.');
		savedPollTimer = Date.now();
		GetBatteryCharge();
		Headset.headsetOn = true;

	}

	if (Headset.headsetOn) {

		// Accounts for null value
		if (readPacket[BatteryChargingIndex] === ChargingVal) {

			if (Headset.headsetCharging !== true) {
				device.log('Headset is charging. Will be unable to estimate battery % while charging.');
			}

			Headset.headsetCharging = true;

		} else if (readPacket[BatteryChargingIndex] === NotChargingVal) {

			if (Headset.headsetCharging) {
				device.log('Stopped charging the headset.');
			}

			Headset.headsetCharging = false;

		}

	}
}

function PollBatteryStatus() {
	if (Date.now() - savedPollTimer < batteryPollInterval) {
		return;
	}

	savedPollTimer = Date.now();
	GetBatteryCharge();
}

function GetBatteryCharge() {
	device.set_endpoint(3, 0x0303, 0xFF53, 0x0003);

	const sendPacket = [];
	sendPacket.push(0x21, 0xFF, 0x05);
	device.write(sendPacket, 20);

	const reportId = [BatteryReportId];
	let readPacket = [];
	readPacket = device.read(reportId, 20);

	// This endpoint is used for other things as well - only continue if we know we are reading the battery info
	if (readPacket[1] !== 0xFF && readPacket[2] !== 0x05) {
		return;
	}

	// The following math logic was copied from https://github.com/hede5562/HeadsetControl/blob/master/src/devices/hyperx_cflight.c
	const batteryVoltage = readPacket[4] | readPacket[3] << 8;
	let batteryPercentage;

	if (batteryVoltage <= 3648) {
		batteryPercentage = 0.00125 * batteryVoltage;
	} else if (batteryVoltage > 3975) {
		batteryPercentage = 100;
	} else {
		batteryPercentage = (0.00000002547505 * Math.pow(batteryVoltage, 4) - 0.0003900299 * Math.pow(batteryVoltage, 3) + 2.238321 * Math.pow(batteryVoltage, 2) - 5706.256 * batteryVoltage + 5452299);
	}

	batteryPercentage = Math.floor(batteryPercentage);

	device.log(`Battery Voltage: ${batteryVoltage} mV`);
	device.log(`Battery Percentage: ~${batteryPercentage}`);

	if (Headset.headsetCharging === true) {
		battery.setBatteryState(BatteryState.Charging);
	} else {

		if (batteryPercentage === 100) {
			battery.setBatteryState(BatteryState.Full);
		} else {
			battery.setBatteryState(BatteryState.Draining);
		}

	}

	battery.setBatteryLevel(batteryPercentage);
}

function CheckMicrophoneStatus() {
	device.set_endpoint(3, 0x0001, 0xFFC0, 0x0002);

	const reportId = [MicrophoneReportId];
	let readPacket = [];
	readPacket = device.read(reportId, 2);

	// When headset turns on, it will always send a follow-up packet saying the microphone is muted,
	// regardless of actual mic state - if we detect the headset turns on, we want to read and ignore the next packet.
	if (readPacket[0] === 0x64 && readPacket[1] === 0x01) {
		readPacket = device.read(reportId, 2);

		return;
	}

	if (readPacket[0] === 0x65) {

		if (readPacket[MicrophoneIndex] === MicrophoneUnmutedVal) {
			device.log("Microphone unmuted.");
		} else if (readPacket[MicrophoneIndex] === MicrophoneMutedVal) {
			device.log("Microphone muted.");
		}

	}
}

export function ImageUrl() {
	return "https://assets.signalrgb.com/devices/brands/hyperx/audio/cloud-flight.png";
}

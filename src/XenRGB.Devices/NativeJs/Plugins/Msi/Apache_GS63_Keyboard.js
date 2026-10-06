export function Name() { return "MSI GS63 Keyboard"; }
export function VendorId() { return 0x1770; }
export function ProductId() { return 0xff00; }
export function Publisher() { return "WhirlwindFX"; }
export function Documentation(){ return "troubleshooting/msi"; }
export function DeviceType(){return "keyboard"}

export function Size() {
	return [9, 3];
}


export function Initialize() {
	return "MSI Init.";
}


export function Validate(endpoint) {
	return true;
}


export function Shutdown(SystemSuspending) {
	return "On Shutdown.";
}


export function Render(overrideColor) {
	// https://github.com/bparker06/msi-keyboard/blob/master/keyboard.cpp

	const packet = [];

	packet[0] = 1;
	packet[1] = 2;
	packet[2] = 64;
	packet[3] = 1;

	const z1 = device.color(1, 0);
	packet[4] = z1[0];
	packet[5] = z1[1];
	packet[6] = z1[2];
	packet[7] = 236;
	device.send_report(packet, 8);

	packet[3] = 2;

	const z2 = device.color(4, 0);
	packet[4] = z2[0];
	packet[5] = z2[1];
	packet[6] = z2[2];
	device.send_report(packet, 8);

	packet[3] = 3;

	const z3 = device.color(7, 0);
	packet[4] = z3[0];
	packet[5] = z3[1];
	packet[6] = z3[2];
	device.send_report(packet, 8);

	packet[3] = 4;
	packet[4] = 0;
	packet[5] = 0;
	packet[6] = 0;

	device.send_report(packet, 8);

	packet[3] = 5;
	device.send_report(packet, 8);

	packet[3] = 6;
	device.send_report(packet, 8);

	packet[3] = 7;
	device.send_report(packet, 8);
}

export function ImageUrl(){
	return "https://assets.signalrgb.com/devices/default/misc/laptop-render.png";
}
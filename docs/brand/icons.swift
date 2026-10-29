// Makes the site's icons (PRD 09) from the duck's face: the tab, Google's search results, a
// bookmark and a phone's home screen. Google asks for a square of 48px or more, so the page
// offers 48, 96 and 192; favicon.ico holds 16, 32 and 48 for anything that asks for that
// address directly; 180 is what an iPhone puts on its home screen. All are cut from the same
// face, so redrawing it means running this again, not editing the files by hand.
//
// Usage, from the repo root:
//   swift -sdk /Library/Developer/CommandLineTools/SDKs/MacOSX15.5.sdk docs/brand/icons.swift \
//       docs/brand/stumpty-face.png OneADay/wwwroot
// (The -sdk is needed only while the newest SDK is ahead of the installed Swift.)
import AppKit

let args = CommandLine.arguments
guard args.count == 3,
      let source = CGImageSourceCreateWithURL(URL(fileURLWithPath: args[1]) as CFURL, nil),
      let face = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
    fatalError("usage: icons.swift <face.png> <wwwroot>")
}
guard face.width == face.height else { fatalError("the face must be square, not \(face.width) × \(face.height)") }
let out = URL(fileURLWithPath: args[2])

/// The face as a PNG, `size` pixels square.
func png(_ size: Int) -> Data {
    let context = CGContext(
        data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
        space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    context.interpolationQuality = .high
    context.draw(face, in: CGRect(x: 0, y: 0, width: size, height: size))
    let rep = NSBitmapImageRep(cgImage: context.makeImage()!)
    return rep.representation(using: .png, properties: [:])!
}

func write(_ data: Data, _ name: String, _ sizes: [Int]) throws {
    try data.write(to: out.appendingPathComponent(name))
    print("wrote \(name): \(sizes.map { "\($0)" }.joined(separator: ", ")) px, \(data.count) bytes")
}

for size in [48, 96, 192] {
    try write(png(size), "favicon-\(size).png", [size])
}
try write(png(180), "apple-touch-icon.png", [180])

// An .ico is a short directory, then the pictures. Every current browser, and Windows since
// Vista, reads PNGs inside.
let sizes = [16, 32, 48]
let pictures = sizes.map(png)
var ico = Data()
func append<T: FixedWidthInteger>(_ value: T) { withUnsafeBytes(of: value.littleEndian) { ico.append(contentsOf: $0) } }
append(UInt16(0)); append(UInt16(1)); append(UInt16(sizes.count))   // reserved, type 1 = icon, count
var offset = 6 + 16 * sizes.count
for (size, picture) in zip(sizes, pictures) {
    append(UInt8(size)); append(UInt8(size))                             // width, height
    append(UInt8(0)); append(UInt8(0))                                   // no palette, reserved
    append(UInt16(1)); append(UInt16(32))                                // planes, bits per pixel
    append(UInt32(picture.count)); append(UInt32(offset))
    offset += picture.count
}
pictures.forEach { ico.append($0) }
try write(ico, "favicon.ico", sizes)

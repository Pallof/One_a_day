// prepare <in> <out> [x y w h]
// Crops (default: centre 16:9), shifts the colours so the picture's cream background matches the
// site's paper (#FCFBF6) when its corners are cream, scales to 1200x675 and saves a JPEG under 200 KB.
// Run it through `tools/pictures/pictures.py prepare`, which compiles it the first time.
import Foundation
import CoreGraphics
import ImageIO
import UniformTypeIdentifiers

let args = CommandLine.arguments
guard args.count == 3 || args.count == 7 else {
    FileHandle.standardError.write("usage: prepare <in> <out> [x y w h]\n".data(using: .utf8)!); exit(2)
}
let src = CGImageSourceCreateWithURL(URL(fileURLWithPath: args[1]) as CFURL, nil)!
let full = CGImageSourceCreateImageAtIndex(src, 0, nil)!
let W = full.width, H = full.height

var rect: CGRect
if args.count == 7 {
    let v = args[3...6].map { Double($0)! }
    rect = CGRect(x: v[0], y: v[1], width: v[2], height: v[3])
} else {
    let h = min(Double(H), Double(W) * 9 / 16), w = h * 16 / 9
    rect = CGRect(x: (Double(W) - w) / 2, y: (Double(H) - h) / 2, width: w, height: h)
}
let cropped = full.cropping(to: rect.integral)!

// Draw into an 8-bit sRGB bitmap so the maths happens on the same values the browser shows.
let cw = cropped.width, ch = cropped.height
let srgb = CGColorSpace(name: CGColorSpace.sRGB)!
var px = [UInt8](repeating: 0, count: cw * ch * 4)
let ctx = CGContext(data: &px, width: cw, height: ch, bitsPerComponent: 8, bytesPerRow: cw * 4,
                    space: srgb, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
ctx.draw(cropped, in: CGRect(x: 0, y: 0, width: cw, height: ch))

// Average each 12x12 corner; a corner counts as background if it's light and nearly grey.
func corner(_ x0: Int, _ y0: Int) -> [Double] {
    var s = [0.0, 0.0, 0.0]
    for y in y0..<(y0 + 12) { for x in x0..<(x0 + 12) {
        let i = (y * cw + x) * 4
        for c in 0..<3 { s[c] += Double(px[i + c]) }
    } }
    return s.map { $0 / 144 }
}
let corners = [corner(4, 4), corner(cw - 16, 4), corner(4, ch - 16), corner(cw - 16, ch - 16)]
let light = corners.filter { c in c.min()! > 190 && c.max()! - c.min()! < 70 }
let target = [252.0, 251.0, 246.0]
// The background is the colour most light corners share; a sky or wall that happens to sit in
// one corner doesn't count.
var cream: [[Double]] = []
for c in light {
    let near = light.filter { o in (0..<3).allSatisfy { abs(o[$0] - c[$0]) < 10 } }
    if near.count > cream.count { cream = near }
}
if cream.count >= 2 {
    let bg = (0..<3).map { c in cream.map { $0[c] }.reduce(0, +) / Double(cream.count) }
    let shift = (0..<3).map { target[$0] - bg[$0] }
    // Move only the background and colours close to it (its anti-aliased edges); fade the shift
    // out as a pixel's colour gets further from the background, so the drawing keeps its colours.
    for i in stride(from: 0, to: px.count, by: 4) {
        let d = (0..<3).map { abs(Double(px[i + $0]) - bg[$0]) }.max()!
        let w = d <= 14 ? 1.0 : d >= 45 ? 0.0 : (45 - d) / 31
        if w > 0 {
            for c in 0..<3 { px[i + c] = UInt8(max(0, min(255, (Double(px[i + c]) + w * shift[c]).rounded()))) }
        }
    }
}
let shifted = ctx.makeImage()!

// Scale to the house size.
let out = CGContext(data: nil, width: 1200, height: 675, bitsPerComponent: 8, bytesPerRow: 0,
                    space: srgb, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
out.interpolationQuality = .high
out.draw(shifted, in: CGRect(x: 0, y: 0, width: 1200, height: 675))
let final = out.makeImage()!

// Highest JPEG quality that stays under 200 KB.
var q = 0.86, data = Data()
repeat {
    let d = NSMutableData()
    let dest = CGImageDestinationCreateWithData(d, UTType.jpeg.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(dest, final, [kCGImageDestinationLossyCompressionQuality: q] as CFDictionary)
    CGImageDestinationFinalize(dest)
    data = d as Data
    q -= 0.04
} while data.count > 200_000 && q > 0.4
try! data.write(to: URL(fileURLWithPath: args[2]))
let fmt = { (v: [Double]) in v.map { String(format: "%.0f", $0) }.joined(separator: ",") }
print("\(URL(fileURLWithPath: args[2]).lastPathComponent): crop \(Int(rect.width))x\(Int(rect.height))+\(Int(rect.minX))+\(Int(rect.minY)), " +
      (cream.count >= 2 ? "background \(fmt(cream[0])) → matched (\(cream.count) cream corners)" : "full-bleed, colours left alone") +
      ", q \(String(format: "%.2f", q + 0.04)), \(data.count / 1024) KB")

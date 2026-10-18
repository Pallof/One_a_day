// Draws the link-preview card (PRD 09): the picture messaging apps show when someone shares a
// link to the site. 1200 × 630, the shape every app expects; the duck beside the name and the
// tagline, on the site's paper colour. Georgia stands in for Lora, as it does on the site
// when the webfont doesn't load. The tagline is large and bold so it can still be read where
// Slack and Discord show the card small; iMessage shows no description, so on iMessage the
// card is the only place that says what the site is.
//
// Usage, from the repo root:
//   swift -sdk /Library/Developer/CommandLineTools/SDKs/MacOSX15.5.sdk docs/brand/link-card.swift \
//       docs/brand/stumpty.png OneADay/wwwroot/img/stumpty-card.png
// (The -sdk is needed only while the newest SDK is ahead of the installed Swift.)
import AppKit

let args = CommandLine.arguments
guard args.count == 3, let duck = NSImage(contentsOfFile: args[1]) else {
    fatalError("usage: link-card.swift <mascot.png> <out.png>")
}

let width = 1200, height = 630
let rep = NSBitmapImageRep(
    bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height, bitsPerSample: 8,
    samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
    bytesPerRow: 0, bitsPerPixel: 0)!
rep.size = NSSize(width: width, height: height)   // one point to a pixel

func colour(_ hex: Int) -> NSColor {
    NSColor(srgbRed: CGFloat((hex >> 16) & 255) / 255, green: CGFloat((hex >> 8) & 255) / 255,
            blue: CGFloat(hex & 255) / 255, alpha: 1)
}
let paper = colour(0xFCFBF6), ink = colour(0x22334D), inkSoft = colour(0x54627A), gold = colour(0xF0A500)

NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)

paper.setFill()
NSRect(x: 0, y: 0, width: width, height: height).fill()

let name = NSAttributedString(string: "Stumpty", attributes: [
    .font: NSFont(name: "Georgia-Bold", size: 116)!, .foregroundColor: ink,
])
let lines = NSMutableParagraphStyle()
lines.lineSpacing = 6
let tagline = NSAttributedString(string: "One puzzle a day to\nsharpen how you think.", attributes: [
    .font: NSFont(name: "AvenirNext-DemiBold", size: 52)!, .foregroundColor: inkSoft, .paragraphStyle: lines,
])

// The duck and the words, centred together. AppKit measures up from the bottom.
let duckHeight: CGFloat = 440
let duckWidth = duckHeight * duck.size.width / duck.size.height
let gap: CGFloat = 56
let textWidth = max(name.size().width, tagline.size().width)
let left = (CGFloat(width) - (duckWidth + gap + textWidth)) / 2
duck.draw(in: NSRect(x: left, y: (CGFloat(height) - duckHeight) / 2, width: duckWidth, height: duckHeight))

// The masthead's gold rule, under the name.
let ruleAbove: CGFloat = 14, rule: CGFloat = 8, ruleBelow: CGFloat = 30
let block = name.size().height + ruleAbove + rule + ruleBelow + tagline.size().height
let x = left + duckWidth + gap
var y = (CGFloat(height) + block) / 2
y -= name.size().height
name.draw(at: NSPoint(x: x, y: y))
y -= ruleAbove + rule
gold.setFill()
NSBezierPath(roundedRect: NSRect(x: x + 4, y: y, width: 96, height: rule), xRadius: 4, yRadius: 4).fill()
y -= ruleBelow + tagline.size().height
tagline.draw(at: NSPoint(x: x + 2, y: y))

NSGraphicsContext.restoreGraphicsState()
try rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: args[2]))
print("wrote \(args[2]) — \(width) × \(height)")

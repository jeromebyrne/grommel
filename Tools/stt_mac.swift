import Foundation
import Speech
import AVFoundation

func fail(_ msg: String) -> Never { fputs(msg + "\n", stderr); exit(1) }

guard CommandLine.arguments.count >= 2 else { fail("Usage: stt_mac <wav_path>") }
let wavPath = CommandLine.arguments[1]
let url = URL(fileURLWithPath: wavPath)
guard FileManager.default.fileExists(atPath: wavPath) else { fail("File not found: \(wavPath)") }

func ensurePcm16Mono16k(_ input: URL) -> URL {
    let tmpOut = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent(UUID().uuidString).appendingPathExtension("wav")
    let task = Process()
    task.launchPath = "/usr/bin/env"
    task.arguments = ["afconvert", "-f", "WAVE", "-d", "LEI16", "-c", "1", "-r", "16000", input.path, tmpOut.path]
    let pipe = Pipe()
    task.standardError = pipe
    task.launch()
    task.waitUntilExit()
    if task.terminationStatus != 0 {
        let errData = pipe.fileHandleForReading.readDataToEndOfFile()
        let errStr = String(data: errData, encoding: .utf8) ?? ""
        fail("afconvert failed: \(errStr)")
    }
    return tmpOut
}

let normalizedUrl = ensurePcm16Mono16k(url)

let sem = DispatchSemaphore(value: 0)
var authOK = false
SFSpeechRecognizer.requestAuthorization { status in
    authOK = (status == .authorized)
    sem.signal()
}
_ = sem.wait(timeout: .now() + 5)
if !authOK { fail("Speech recognition not authorized (check Privacy > Speech Recognition).") }

guard let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "en_US")) else {
    fail("No recognizer for locale.")
}

let request = SFSpeechURLRecognitionRequest(url: normalizedUrl)
var finalText = ""
var done = false
var lastError: Error?

recognizer.recognitionTask(with: request) { result, error in
    if let r = result {
        finalText = r.bestTranscription.formattedString
        if r.isFinal { done = true }
    }
    if let err = error {
        lastError = err
        done = true
    }
    if done { sem.signal() }
}

_ = sem.wait(timeout: .now() + 60)
if finalText.isEmpty {
    if let err = lastError {
        fail("No transcript produced. Error: \(err.localizedDescription)")
    } else {
        fail("No transcript produced.")
    }
}
print(finalText)

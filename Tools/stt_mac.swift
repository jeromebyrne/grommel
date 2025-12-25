import Foundation
import Speech
import AVFoundation

func fail(_ msg: String) -> Never { fputs(msg + "\n", stderr); exit(1) }

guard CommandLine.arguments.count >= 2 else { fail("Usage: stt_mac <wav_path>") }
let wavPath = CommandLine.arguments[1]
let url = URL(fileURLWithPath: wavPath)
guard FileManager.default.fileExists(atPath: wavPath) else { fail("File not found: \(wavPath)") }

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

let request = SFSpeechURLRecognitionRequest(url: url)
var finalText = ""
var done = false

recognizer.recognitionTask(with: request) { result, error in
    if let r = result {
        finalText = r.bestTranscription.formattedString
        if r.isFinal { done = true }
    }
    if error != nil { done = true }
    if done { sem.signal() }
}

_ = sem.wait(timeout: .now() + 60)
if finalText.isEmpty { fail("No transcript produced.") }
print(finalText)

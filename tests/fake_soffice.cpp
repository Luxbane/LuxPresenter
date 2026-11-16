// Stand-in for "soffice --headless --convert-to pdf" used by the presentation tests (LibreOffice is an external
// dependency and is not installed on the build machine). Behaviour is selected with environment variables:
//   FAKE_SOFFICE_PDF   PDF to deliver as the conversion result
//   FAKE_SOFFICE_MODE  "" (success) | fail | hang | nooutput
//   FAKE_SOFFICE_LOG   file that receives the command line (one argument per line)
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <string>
#include <thread>
#include <vector>

namespace fs = std::filesystem;

int main(int argc, char** argv)
{
    std::vector<std::string> args(argv + 1, argv + argc);
    if (const char* log = std::getenv("FAKE_SOFFICE_LOG")) {
        std::ofstream out(log, std::ios::app);
        for (const auto& a : args)
            out << a << "\n";
        out << "---\n";
    }
    std::string input, outdir;
    for (size_t i = 0; i < args.size(); ++i) {
        if (args[i] == "--convert-to" && i + 2 < args.size())
            input = args[i + 2];
        if (args[i] == "--outdir" && i + 1 < args.size())
            outdir = args[i + 1];
    }
    const char* modeEnv = std::getenv("FAKE_SOFFICE_MODE");
    const std::string mode = modeEnv ? modeEnv : "";
    if (mode == "fail") {
        std::fprintf(stderr, "Error: source file could not be loaded\n");
        return 3;
    }
    if (mode == "hang") {
        std::this_thread::sleep_for(std::chrono::seconds(60));
        return 0;
    }
    if (mode == "nooutput")
        return 0;
    const char* pdf = std::getenv("FAKE_SOFFICE_PDF");
    if (input.empty() || outdir.empty() || !pdf)
        return 2;
    std::error_code ec;
    fs::create_directories(outdir, ec);
    const fs::path out = fs::path(outdir) / (fs::path(input).stem().string() + ".pdf");
    fs::copy_file(pdf, out, fs::copy_options::overwrite_existing, ec);
    if (!ec)
        fs::last_write_time(out, fs::file_time_type::clock::now(), ec);   // CopyFile keeps the source time; a real conversion is "now"
    return ec ? 4 : 0;
}

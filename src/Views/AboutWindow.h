#pragma once

#include <QDialog>

namespace Lux {

// "About LuxPresenter" dialog (AboutWindow.xaml): 460x330, logo, version, links, shortcuts.
class AboutWindow : public QDialog
{
    Q_OBJECT
public:
    explicit AboutWindow(QWidget* parent = nullptr);

    static constexpr const char* kKofiUrl = "https://ko-fi.com/luxbane";
};

} // namespace Lux

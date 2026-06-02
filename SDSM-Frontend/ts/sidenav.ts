function sidebarButtonClick(): void {
    var sidenav = document.getElementById("globalSidenav");
    if (sidenav) {
        if (parseInt(sidenav.style.width) != 0) {
            sidenav.style.width = "0px";
        } else {
            sidenav.style.width = "200px"
        }
    }
}

var app = angular.module('app', ['ngRoute']);

app.config(function ($routeProvider) {

    /// this is an example of how you can write unreadable javascript with any framework :(

    var regions = ["us", "eu", "kr", "tw", "cn"];
    var brackets = {
        "2v2": 2,
        "3v3": 3,
        "5v5": 5,
        "rbg": 10
    };

    var routeProvider = $routeProvider;

    for (var ir in regions)
        for (var bracket in brackets) {

            var region = regions[ir];

            // binding closures to loop variables
            var regionProvider = function(r) { return function() { return r; }; }
            var bracketProvider = function(b) { return function() {
                return {
                    text: b,
                    size: brackets[b]
                };
            }; }

            routeProvider = routeProvider
                .when('/' + region + '/' + bracket,
                {
                    controller: "ApiController",
                    templateUrl: "app/templates/" + bracket + ".html",
                    resolve: {
                        region: regionProvider(region),
                        bracket: bracketProvider(bracket)
                    }
                });

        };

    for (var ir in regions) {
        var region = regions[ir];

        var regionRoot = '/' + region;

        routeProvider = routeProvider
            .when(regionRoot,
            {
                redirectTo: regionRoot + "/3v3"
            });

        routeProvider = routeProvider
            .when(regionRoot+'/:filter',
            {
                redirectTo: regionRoot + "/3v3"
            });
    }
    
    routeProvider.otherwise({ redirectTo: '/us/3v3' });
});